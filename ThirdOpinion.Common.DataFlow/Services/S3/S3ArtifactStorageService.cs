using System.Text;
using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThirdOpinion.Common.DataFlow.Artifacts;
using ThirdOpinion.Common.DataFlow.EntityFramework.Entities;
using ThirdOpinion.Common.DataFlow.Models;
using ThirdOpinion.Common.DataFlow.Services.EfCore;
using ThirdOpinion.DataFlow.Artifacts.Models;

namespace ThirdOpinion.Common.DataFlow.Services.S3;

/// <summary>
/// Stores pipeline artifacts in Amazon S3 while persisting metadata to the Entity Framework store.
/// </summary>
public class S3ArtifactStorageService : IArtifactStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly PipelineContextPool _contextPool;
    private readonly ILogger<S3ArtifactStorageService> _logger;
    private readonly S3ArtifactStorageOptions _options;
    private readonly SemaphoreSlim _bucketGate = new(1, 1);
    private bool _bucketChecked;

    public S3ArtifactStorageService(
        IAmazonS3 s3Client,
        PipelineContextPool contextPool,
        IOptions<S3ArtifactStorageOptions> options,
        ILogger<S3ArtifactStorageService> logger)
    {
        _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
        _contextPool = contextPool ?? throw new ArgumentNullException(nameof(contextPool));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrWhiteSpace(_options.BucketName))
        {
            throw new ArgumentException("S3 bucket name must be provided.", nameof(options));
        }
    }

    public async Task<List<ArtifactSaveResult>> SaveBatchAsync(List<ArtifactSaveRequest> requests, CancellationToken ct)
    {
        if (requests == null)
        {
            throw new ArgumentNullException(nameof(requests));
        }

        if (requests.Count == 0)
        {
            return new List<ArtifactSaveResult>();
        }

        await EnsureBucketExistsAsync(ct).ConfigureAwait(false);

        var dbContext = await _contextPool.RentAsync(ct).ConfigureAwait(false);
        try
        {
            var resourceRunIds = requests.Select(r => r.ResourceRunId).Distinct().ToList();
            var resourceRuns = await dbContext.ResourceRuns
                .AsNoTracking()
                .Where(rr => resourceRunIds.Contains(rr.ResourceRunId))
                .Select(rr => new ResourceRunSnapshot(rr.ResourceRunId, rr.PipelineRunId, rr.ResourceId ?? string.Empty))
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var resourceRunMap = resourceRuns.ToDictionary(rr => rr.ResourceRunId);
            var results = new List<ArtifactSaveResult>(requests.Count);
            var successfulUploads = new List<(ArtifactSaveRequest Request, S3UploadResult Upload, ResourceRunSnapshot Snapshot)>();

            foreach (var request in requests)
            {
                ct.ThrowIfCancellationRequested();

                var storageType = request.StorageTypeOverride ?? ArtifactStorageType.S3;
                if (storageType != ArtifactStorageType.S3)
                {
                    var message = $"Artifact storage override '{storageType}' is not supported by the S3 artifact storage service.";
                    _logger.LogWarning("{Message} Step: {Step}, Artifact: {Artifact}", message, request.StepName, request.ArtifactName);
                    results.Add(new ArtifactSaveResult
                    {
                        Success = false,
                        ErrorMessage = message,
                        Metadata = new Dictionary<string, object?>
                        {
                            ["storageType"] = storageType.ToString(),
                            ["stepName"] = request.StepName,
                            ["artifactName"] = request.ArtifactName
                        }
                    });
                    continue;
                }

                if (!resourceRunMap.TryGetValue(request.ResourceRunId, out var resourceRun))
                {
                    var message = $"ResourceRun {request.ResourceRunId} not found while saving artifact.";
                    _logger.LogWarning("{Message} Step: {Step}, Artifact: {Artifact}", message, request.StepName, request.ArtifactName);
                    results.Add(new ArtifactSaveResult
                    {
                        Success = false,
                        ErrorMessage = message,
                        Metadata = new Dictionary<string, object?>
                        {
                            ["resourceRunId"] = request.ResourceRunId,
                            ["stepName"] = request.StepName,
                            ["artifactName"] = request.ArtifactName
                        }
                    });
                    continue;
                }

                try
                {
                    var upload = await UploadToS3Async(request, resourceRun, ct).ConfigureAwait(false);
                    successfulUploads.Add((request, upload, resourceRun));
                    results.Add(new ArtifactSaveResult
                    {
                        Success = true,
                        StoragePath = upload.S3Uri,
                        Metadata = new Dictionary<string, object?>
                        {
                            ["bucketName"] = _options.BucketName,
                            ["s3Key"] = upload.S3Key,
                            ["size"] = upload.Size,
                            ["contentType"] = upload.ContentType,
                            ["artifactName"] = upload.ArtifactName,
                            ["resourceRunId"] = request.ResourceRunId,
                            ["stepName"] = request.StepName
                        }
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to upload artifact to S3. Step: {Step}, Artifact: {Artifact}, ResourceRunId: {ResourceRunId}",
                        request.StepName, request.ArtifactName, request.ResourceRunId);
                    results.Add(new ArtifactSaveResult
                    {
                        Success = false,
                        ErrorMessage = ex.Message,
                        Metadata = new Dictionary<string, object?>
                        {
                            ["resourceRunId"] = request.ResourceRunId,
                            ["stepName"] = request.StepName,
                            ["artifactName"] = request.ArtifactName,
                            ["exception"] = ex.GetType().Name
                        }
                    });
                }
            }

            if (successfulUploads.Count > 0)
            {
                var now = DateTime.UtcNow;
                var entities = successfulUploads.Select(upload => CreateArtifactEntity(upload.Request, upload.Upload, upload.Snapshot, now)).ToList();
                await dbContext.Artifacts.AddRangeAsync(entities, ct).ConfigureAwait(false);
                await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
            }

            return results;
        }
        finally
        {
            _contextPool.Return(dbContext);
        }
    }

    private async Task<S3UploadResult> UploadToS3Async(ArtifactSaveRequest request, ResourceRunSnapshot resourceRun, CancellationToken ct)
    {
        var payload = PreparePayload(request);
        var key = BuildS3Key(resourceRun, request.StepName, payload.FinalFileName);

        using var stream = new MemoryStream(payload.Bytes, writable: false);
        var putRequest = new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = key,
            InputStream = stream,
            ContentType = payload.ContentType
        };

        putRequest.Metadata["PipelineRunId"] = resourceRun.PipelineRunId.ToString();
        putRequest.Metadata["ResourceId"] = resourceRun.ResourceId ?? string.Empty;
        putRequest.Metadata["ResourceRunId"] = resourceRun.ResourceRunId.ToString();
        putRequest.Metadata["StepName"] = request.StepName;
        putRequest.Metadata["ArtifactName"] = payload.FinalFileName;
        putRequest.Metadata["PayloadSize"] = payload.Bytes.Length.ToString();
        putRequest.Metadata["IsStringData"] = payload.IsStringData.ToString();

        await _s3Client.PutObjectAsync(putRequest, ct).ConfigureAwait(false);

        var s3Uri = $"s3://{_options.BucketName}/{key}";

        _logger.LogDebug("Saved artifact to S3 for resource {ResourceId}, step {Step}, artifact {Artifact} ({Size} bytes)",
            resourceRun.ResourceId, request.StepName, payload.FinalFileName, payload.Bytes.Length);

        return new S3UploadResult(s3Uri, key, payload.Bytes.Length, payload.ContentType, payload.IsStringData, payload.FinalFileName);
    }

    private ArtifactEntity CreateArtifactEntity(
        ArtifactSaveRequest request,
        S3UploadResult upload,
        ResourceRunSnapshot resourceRun,
        DateTime timestampUtc)
    {
        var metadata = JsonSerializer.Serialize(new
        {
            size = upload.Size,
            contentType = upload.ContentType,
            isStringData = upload.IsStringData,
            artifactName = upload.ArtifactName,
            s3Key = upload.S3Key,
            pipelineRunId = resourceRun.PipelineRunId,
            resourceId = resourceRun.ResourceId,
            uploadedAt = timestampUtc
        });

        return new ArtifactEntity
        {
            ArtifactId = Guid.NewGuid(),
            ResourceRunId = request.ResourceRunId,
            StepName = request.StepName,
            ArtifactName = upload.ArtifactName,
            StorageType = ArtifactStorageType.S3,
            StoragePath = upload.S3Uri,
            DataJson = null,
            MetadataJson = metadata,
            CreatedAt = timestampUtc
        };
    }

    private Payload PreparePayload(ArtifactSaveRequest request)
    {
        if (request.Data is string stringData)
        {
            var baseName = string.IsNullOrWhiteSpace(request.ArtifactName)
                ? "artifact"
                : request.ArtifactName;

            var finalName = baseName.Contains('.', StringComparison.Ordinal)
                ? baseName
                : $"{baseName}.txt";

            var contentType = GetTextContentType(finalName);
            var bytes = Encoding.UTF8.GetBytes(stringData);

            return new Payload(bytes, finalName, contentType, true);
        }

        var jsonName = string.IsNullOrWhiteSpace(request.ArtifactName)
            ? "artifact.json"
            : (request.ArtifactName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                ? request.ArtifactName
                : $"{request.ArtifactName}.json");

        var serialized = JsonSerializer.SerializeToUtf8Bytes(request.Data, request.Data.GetType());
        return new Payload(serialized, jsonName, "application/json", false);
    }

    private static string GetTextContentType(string artifactName)
    {
        var extension = Path.GetExtension(artifactName).ToLowerInvariant();
        return extension switch
        {
            ".md" => "text/markdown",
            ".html" or ".htm" => "text/html",
            ".json" => "application/json",
            ".txt" => "text/plain",
            _ => "text/plain"
        };
    }

    private string BuildS3Key(ResourceRunSnapshot resourceRun, string stepName, string artifactName)
    {
        var prefix = NormalizePrefix(_options.KeyPrefix);
        var builder = new StringBuilder();

        if (!string.IsNullOrEmpty(prefix))
        {
            builder.Append(prefix).Append('/');
        }

        builder.Append("runs/")
            .Append(resourceRun.PipelineRunId.ToString())
            .Append('/')
            .Append(SanitizeSegment(resourceRun.ResourceId))
            .Append('/')
            .Append(SanitizeSegment(stepName))
            .Append('/')
            .Append(artifactName);

        return builder.ToString();
    }

    private static string NormalizePrefix(string? prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return string.Empty;
        }

        return prefix.Trim().Trim('/').Replace('\\', '/');
    }

    private static string SanitizeSegment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        var sanitized = value.Trim().Replace('\\', '/');
        var builder = new StringBuilder(sanitized.Length);

        foreach (var c in sanitized)
        {
            if (char.IsLetterOrDigit(c) || c is '-' or '_' or '/' or '.')
            {
                builder.Append(c);
            }
            else
            {
                builder.Append('-');
            }
        }

        return builder.ToString().Trim('-');
    }

    private async Task EnsureBucketExistsAsync(CancellationToken ct)
    {
        if (_bucketChecked || !_options.EnsureBucketExists)
        {
            _bucketChecked = true;
            return;
        }

        await _bucketGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_bucketChecked)
            {
                return;
            }

            try
            {
                await _s3Client.GetBucketLocationAsync(_options.BucketName, ct).ConfigureAwait(false);
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogInformation("S3 bucket {Bucket} not found. Creating...", _options.BucketName);
                var putBucketRequest = new PutBucketRequest
                {
                    BucketName = _options.BucketName,
                    UseClientRegion = true
                };

                await _s3Client.PutBucketAsync(putBucketRequest, ct).ConfigureAwait(false);
                _logger.LogInformation("S3 bucket {Bucket} created successfully.", _options.BucketName);
            }

            _bucketChecked = true;
        }
        finally
        {
            _bucketGate.Release();
        }
    }

    /// <summary>
    /// Retrieves artifact content from S3 using its URI.
    /// </summary>
    public async Task<string?> RetrieveAsync(string? s3Uri, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(s3Uri))
        {
            return null;
        }

        try
        {
            var uri = new Uri(s3Uri);
            var bucket = uri.Host;
            var key = uri.AbsolutePath.TrimStart('/');

            var request = new GetObjectRequest
            {
                BucketName = bucket,
                Key = key
            };

            using var response = await _s3Client.GetObjectAsync(request, ct).ConfigureAwait(false);
            using var reader = new StreamReader(response.ResponseStream);
            return await reader.ReadToEndAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve artifact from S3: {Uri}", s3Uri);
            throw;
        }
    }

    private readonly record struct ResourceRunSnapshot(Guid ResourceRunId, Guid PipelineRunId, string ResourceId);

    private readonly record struct Payload(byte[] Bytes, string FinalFileName, string ContentType, bool IsStringData);

    private readonly record struct S3UploadResult(string S3Uri, string S3Key, int Size, string ContentType, bool IsStringData, string ArtifactName);
}

