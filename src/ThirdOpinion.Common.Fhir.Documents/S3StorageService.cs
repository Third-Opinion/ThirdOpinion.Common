using System.Net;
using ThirdOpinion.Common.Aws.S3;
using Amazon.S3;
using Amazon.S3.Model;
using ThirdOpinion.Common.Aws.HealthLake.Configuration;
using ThirdOpinion.Common.Logging;
using ThirdOpinion.Common.Misc.RateLimiting;
using ThirdOpinion.Common.Misc.Retry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ThirdOpinion.Common.Fhir.Documents;

/// <summary>
/// Service for managing S3 storage operations including file existence checking and uploads
/// </summary>
public class S3StorageService : IS3StorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly ILogger<S3StorageService> _logger;
    private readonly ICorrelationIdProvider _correlationIdProvider;
    private readonly string _s3BucketName;

    public S3StorageService(
        IAmazonS3 s3Client,
        ILogger<S3StorageService> logger,
        ICorrelationIdProvider correlationIdProvider,
        IOptions<HealthLakeConfig> healthLakeConfig)
    {
        _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _correlationIdProvider = correlationIdProvider ?? throw new ArgumentNullException(nameof(correlationIdProvider));
        _s3BucketName = healthLakeConfig?.Value?.S3BucketName ?? "healthlake-documents";
    }

    /// <inheritdoc />
    public async Task<bool> CheckFileExistsAsync(string bucket, string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(bucket))
            throw new ArgumentException("Bucket cannot be null or whitespace", nameof(bucket));
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be null or whitespace", nameof(key));

        var correlationId = _correlationIdProvider.GetCorrelationId();

        _logger.LogDebug("Checking if file exists: s3://{Bucket}/{Key} [CorrelationId: {CorrelationId}]",
            bucket, key, correlationId);

        try
        {
            // Direct AWS SDK call with built-in retry
            try
            {
                var request = new GetObjectMetadataRequest
                {
                    BucketName = bucket,
                    Key = key
                };

                await _s3Client.GetObjectMetadataAsync(request, cancellationToken);

                _logger.LogDebug("File existence check result: s3://{Bucket}/{Key} exists: {Exists} [CorrelationId: {CorrelationId}]",
                    bucket, key, true, correlationId);

                return true;
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogDebug("File existence check result: s3://{Bucket}/{Key} exists: {Exists} [CorrelationId: {CorrelationId}]",
                    bucket, key, false, correlationId);

                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking file existence: s3://{Bucket}/{Key} [CorrelationId: {CorrelationId}]",
                bucket, key, correlationId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<string> UploadFileAsync(
        string bucket,
        string key,
        Stream content,
        Dictionary<string, string>? metadata = null,
        List<Tag>? tags = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(bucket))
            throw new ArgumentException("Bucket cannot be null or whitespace", nameof(bucket));
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be null or whitespace", nameof(key));
        if (content == null)
            throw new ArgumentNullException(nameof(content));

        var correlationId = _correlationIdProvider.GetCorrelationId();

        _logger.LogDebug("Uploading file: s3://{Bucket}/{Key}, Size: {Size} bytes [CorrelationId: {CorrelationId}]",
            bucket, key, content.Length, correlationId);

        try
        {
            // Direct AWS SDK call with built-in retry
            var request = new PutObjectRequest
            {
                BucketName = bucket,
                Key = key,
                InputStream = content,
                ContentType = DetermineContentType(key),
                StorageClass = S3StorageClass.StandardInfrequentAccess // TODO: Make configurable
            };

            // Add metadata if provided
            if (metadata != null)
            {
                foreach (var kvp in metadata)
                {
                    request.Metadata.Add(kvp.Key, kvp.Value);
                }
            }

            // Add tags if provided
            if (tags != null && tags.Count > 0)
            {
                request.TagSet = tags;
            }

            // Add correlation ID as metadata
            request.Metadata.Add("CorrelationId", correlationId);

            // Configure server-side encryption
            // TODO: Make encryption configurable
            request.ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256;

            var uploadResult = await _s3Client.PutObjectAsync(request, cancellationToken);

            var s3Location = $"s3://{bucket}/{key}";

            _logger.LogInformation("Successfully uploaded file: {S3Location}, ETag: {ETag} [CorrelationId: {CorrelationId}]",
                s3Location, uploadResult.ETag, correlationId);

            return s3Location;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file: s3://{Bucket}/{Key} [CorrelationId: {CorrelationId}]",
                bucket, key, correlationId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> HandleForceOverwriteAsync(bool forceMode, string bucket, string key, CancellationToken cancellationToken = default)
    {
        if (!forceMode)
        {
            var exists = await CheckFileExistsAsync(bucket, key, cancellationToken);
            if (exists)
            {
                var correlationId = _correlationIdProvider.GetCorrelationId();
                _logger.LogWarning("File already exists and force mode is disabled: s3://{Bucket}/{Key} [CorrelationId: {CorrelationId}]",
                    bucket, key, correlationId);
                return false; // Cannot proceed
            }
        }

        return true; // Can proceed (either force mode is enabled or file doesn't exist)
    }

    /// <inheritdoc />
    public async Task<S3FileInfo?> GetFileInfoAsync(string bucket, string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(bucket))
            throw new ArgumentException("Bucket cannot be null or whitespace", nameof(bucket));
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be null or whitespace", nameof(key));

        var correlationId = _correlationIdProvider.GetCorrelationId();

        try
        {
            // Direct AWS SDK call with built-in retry
            try
            {
                var request = new GetObjectMetadataRequest
                {
                    BucketName = bucket,
                    Key = key
                };

                var metadata = await _s3Client.GetObjectMetadataAsync(request, cancellationToken);

                return new S3FileInfo
                {
                    Bucket = bucket,
                    Key = key,
                    Size = metadata.ContentLength,
                    LastModified = metadata.LastModified ?? DateTime.MinValue,
                    ETag = metadata.ETag,
                    ContentType = metadata.Headers.ContentType,
                    Metadata = metadata.Metadata.Keys.ToDictionary(k => k, k => metadata.Metadata[k])
                };
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting file info: s3://{Bucket}/{Key} [CorrelationId: {CorrelationId}]",
                bucket, key, correlationId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteFileAsync(string bucket, string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(bucket))
            throw new ArgumentException("Bucket cannot be null or whitespace", nameof(bucket));
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be null or whitespace", nameof(key));

        var correlationId = _correlationIdProvider.GetCorrelationId();

        _logger.LogDebug("Deleting file: s3://{Bucket}/{Key} [CorrelationId: {CorrelationId}]",
            bucket, key, correlationId);

        try
        {
            // Direct AWS SDK call with built-in retry
            var request = new DeleteObjectRequest
            {
                BucketName = bucket,
                Key = key
            };

            await _s3Client.DeleteObjectAsync(request, cancellationToken);

            _logger.LogInformation("Successfully deleted file: s3://{Bucket}/{Key} [CorrelationId: {CorrelationId}]",
                bucket, key, correlationId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file: s3://{Bucket}/{Key} [CorrelationId: {CorrelationId}]",
                bucket, key, correlationId);
            return false;
        }
    }


    private static string DetermineContentType(string key)
    {
        var extension = Path.GetExtension(key).ToLowerInvariant();

        return extension switch
        {
            ".json" => "application/json",
            ".md" => "text/markdown",
            ".txt" => "text/plain",
            ".html" => "text/html",
            ".xml" => "application/xml",
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            _ => "application/octet-stream"
        };
    }
}

/// <summary>
/// Information about an S3 file
/// </summary>
public class S3FileInfo
{
    /// <summary>
    /// S3 bucket name
    /// </summary>
    public string Bucket { get; set; } = string.Empty;

    /// <summary>
    /// S3 object key
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// File size in bytes
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Last modified timestamp
    /// </summary>
    public DateTime LastModified { get; set; }

    /// <summary>
    /// S3 ETag
    /// </summary>
    public string ETag { get; set; } = string.Empty;

    /// <summary>
    /// Content type
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// Object metadata
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// Full S3 URI
    /// </summary>
    public string S3Uri => $"s3://{Bucket}/{Key}";
}