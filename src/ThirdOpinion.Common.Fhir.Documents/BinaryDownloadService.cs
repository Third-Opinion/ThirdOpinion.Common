using System.Net;
using ThirdOpinion.Common.Aws.S3;
using ThirdOpinion.Common.Aws.HealthLake.Http;
using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;
using ThirdOpinion.Common.Aws.HealthLake.Configuration;
using ThirdOpinion.Common.Fhir.Documents.Exceptions;
using ThirdOpinion.Common.Fhir.Documents.Models;
using ThirdOpinion.Common.Aws.HealthLake;
using ThirdOpinion.Common.Aws.HealthLake;
using ThirdOpinion.Common.Logging;
using ThirdOpinion.Common.Misc.RateLimiting;
using ThirdOpinion.Common.Misc.Retry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ThirdOpinion.Common.Fhir.Documents;

/// <summary>
/// Service for downloading Binary resources from HealthLake with streaming support
/// </summary>
public class BinaryDownloadService : IBinaryDownloadService
{
    private readonly IAmazonS3 _s3Client;
    private readonly IHealthLakeHttpService _healthLakeHttpService;
    private readonly ILogger<BinaryDownloadService> _logger;
    private readonly ICorrelationIdProvider _correlationIdProvider;
    private readonly IRateLimiterService _rateLimiterService;
    private readonly IRetryPolicyService _retryPolicyService;
    private readonly INotFoundBinaryTracker _notFoundTracker;
    private readonly HealthLakeConfig _healthLakeConfig;
    private readonly string _s3BucketName;

    public BinaryDownloadService(
        IAmazonS3 s3Client,
        IHealthLakeHttpService healthLakeHttpService,
        ILogger<BinaryDownloadService> logger,
        ICorrelationIdProvider correlationIdProvider,
        IRateLimiterService rateLimiterService,
        IRetryPolicyService retryPolicyService,
        INotFoundBinaryTracker notFoundTracker,
        IOptions<HealthLakeConfig> healthLakeConfig)
    {
        _s3Client = s3Client;
        _healthLakeHttpService = healthLakeHttpService;
        _logger = logger;
        _correlationIdProvider = correlationIdProvider;
        _rateLimiterService = rateLimiterService;
        _retryPolicyService = retryPolicyService;
        _notFoundTracker = notFoundTracker;
        _healthLakeConfig = healthLakeConfig.Value;
        _s3BucketName = "healthlake-documents"; // TODO: Add S3 configuration to HealthLakeConfig
    }

    public async Task<BinaryDownloadResult> DownloadBinaryToS3Async(
        string binaryId,
        string s3Bucket,
        string s3Key,
        S3TagSet? s3TagSet = null,
        string? patientId = null,
        string? documentReferenceId = null,
        CancellationToken cancellationToken = default)
    {
        var correlationId = _correlationIdProvider.GetCorrelationId();
        var startTime = DateTime.UtcNow;

        _logger.LogInformation("Starting binary download: BinaryId={BinaryId}, S3Key={S3Key} [CorrelationId: {CorrelationId}]",
            binaryId, s3Key, correlationId);

        try
        {
            // Apply rate limiting
            var rateLimiter = _rateLimiterService.GetRateLimiter("HealthLake");
            await rateLimiter.WaitAsync(cancellationToken);

            // Build the Binary resource URL
            var binaryUrl = BuildBinaryUrl(binaryId);

            _logger.LogInformation("Downloading binary from HealthLake URL: {BinaryUrl} [CorrelationId: {CorrelationId}]",
                binaryUrl, correlationId);

            // Create the HTTP request
            using var request = new HttpRequestMessage(HttpMethod.Get, binaryUrl);

            // Execute with retry policy
            var retryPolicy = _retryPolicyService.GetCombinedPolicy("HealthLake");

            _logger.LogDebug("Starting HTTP request for binary {BinaryId} [CorrelationId: {CorrelationId}]",
                binaryId, correlationId);

            var response = await retryPolicy.ExecuteAsync(async () =>
            {
                _logger.LogDebug("Executing HTTP request attempt for binary {BinaryId} [CorrelationId: {CorrelationId}]",
                    binaryId, correlationId);

                // Clone request for potential retries
                var clonedRequest = await _healthLakeHttpService.CloneHttpRequestAsync(request);

                // Send the request with AWS credentials
                var httpResponse = await _healthLakeHttpService.SendSignedRequestAsync(clonedRequest, cancellationToken);

                _logger.LogDebug("HTTP response received for binary {BinaryId}: {StatusCode} [CorrelationId: {CorrelationId}]",
                    binaryId, httpResponse.StatusCode, correlationId);

                if (!httpResponse.IsSuccessStatusCode)
                {
                    var errorContent = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

                    // Handle 404 NotFound errors specifically
                    if (httpResponse.StatusCode == HttpStatusCode.NotFound)
                    {
                        _logger.LogWarning("Binary {BinaryId} not found (404) [CorrelationId: {CorrelationId}]",
                            binaryId, correlationId);

                        // Track the NotFound binary with full URL
                        await _notFoundTracker.TrackNotFoundBinaryAsync(
                            binaryId,
                            binaryUrl,
                            patientId,
                            documentReferenceId,
                            correlationId,
                            cancellationToken);
                    }

                    _logger.LogError("HTTP request failed for binary {BinaryId}: {StatusCode} - {ErrorContent} [CorrelationId: {CorrelationId}]",
                        binaryId, httpResponse.StatusCode, errorContent, correlationId);
                    throw new DocumentDownloadException(
                        $"Failed to download Binary {binaryId}: {httpResponse.StatusCode} - {errorContent}",
                        httpResponse.StatusCode == HttpStatusCode.TooManyRequests,
                        ErrorCategory.Infrastructure);
                }
                return httpResponse;
            });

            _logger.LogDebug("HTTP request completed successfully for binary {BinaryId}, proceeding to S3 upload [CorrelationId: {CorrelationId}]",
                binaryId, correlationId);

            using (response)
            {
                _logger.LogDebug("Entered S3 upload section for binary {BinaryId} [CorrelationId: {CorrelationId}]",
                    binaryId, correlationId);

                // Get content information
                var contentType = response.Content.Headers.ContentType?.MediaType;
                var fileName = ExtractFileNameFromHeaders(response.Headers);
                var contentLength = response.Content.Headers.ContentLength;

                _logger.LogDebug("Binary response headers: ContentType={ContentType}, FileName={FileName}, ContentLength={ContentLength} [CorrelationId: {CorrelationId}]",
                    contentType, fileName, contentLength, correlationId);

                // Read the JSON response and extract the base64 data
                var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);

                _logger.LogDebug("Parsing FHIR Binary JSON response for binary {BinaryId} [CorrelationId: {CorrelationId}]",
                    binaryId, correlationId);

                var binaryInfo = ExtractBinaryDataFromFhirJson(jsonContent, binaryId, correlationId);

                // Stream the decoded binary data to S3
                using var binaryStream = new MemoryStream(binaryInfo.Data);

                // Use the content type from the FHIR Binary resource
                var s3ContentType = binaryInfo.ContentType ?? "application/octet-stream";

                var multipartThreshold = 100 * 1024 * 1024; // 100MB - TODO: Make configurable
                var uploadResult = binaryInfo.Data.Length > multipartThreshold
                    ? await UploadMultipartToS3Async(binaryStream, s3Bucket, s3Key, s3ContentType, s3TagSet, cancellationToken)
                    : await UploadSinglePartToS3Async(binaryStream, s3Bucket, s3Key, s3ContentType, s3TagSet, cancellationToken);

                var result = new BinaryDownloadResult
                {
                    BinaryId = binaryId,
                    S3Key = s3Key,
                    SizeBytes = binaryInfo.Data.Length, // Use actual binary data size
                    ContentType = binaryInfo.ContentType, // Use content type from FHIR Binary
                    FileName = fileName,
                    DownloadDuration = DateTime.UtcNow - startTime,
                    S3ETag = uploadResult.ETag,
                    UsedMultipartUpload = uploadResult.UsedMultipartUpload
                };

                // Log successful S3 upload with file details
                _logger.LogInformation("Successfully uploaded file to S3: " +
                    "FileName={FileName}, ContentType={ContentType}, Size={SizeBytes} bytes, " +
                    "S3Key={S3Key}, BinaryId={BinaryId}, Duration={Duration}ms [CorrelationId: {CorrelationId}]",
                    fileName ?? "unknown", binaryInfo.ContentType, result.SizeBytes,
                    s3Key, binaryId, result.DownloadDuration.TotalMilliseconds, correlationId);

                return result;
            }
        }
        catch (Exception ex) when (!(ex is DocumentDownloadException))
        {
            _logger.LogError(ex, "Unexpected error downloading binary {BinaryId} [CorrelationId: {CorrelationId}]", binaryId, correlationId);
            throw new DocumentDownloadException($"Failed to download binary {binaryId}: {ex.Message}", ex, true, ErrorCategory.Infrastructure);
        }
    }

    public async Task<BinaryContent> DownloadBinaryToMemoryAsync(
        string binaryId,
        string? patientId = null,
        string? documentReferenceId = null,
        CancellationToken cancellationToken = default)
    {
        var correlationId = _correlationIdProvider.GetCorrelationId();

        _logger.LogInformation("Downloading binary to memory: BinaryId={BinaryId} [CorrelationId: {CorrelationId}]", binaryId, correlationId);

        try
        {
            // Apply rate limiting
            var rateLimiter = _rateLimiterService.GetRateLimiter("HealthLake");
            await rateLimiter.WaitAsync(cancellationToken);

            // Build the Binary resource URL
            var binaryUrl = BuildBinaryUrl(binaryId);

            // Create the HTTP request
            using var request = new HttpRequestMessage(HttpMethod.Get, binaryUrl);

            // Execute with retry policy
            var retryPolicy = _retryPolicyService.GetCombinedPolicy("HealthLake");
            var response = await retryPolicy.ExecuteAsync(async () =>
            {
                // Clone request for potential retries
                var clonedRequest = await _healthLakeHttpService.CloneHttpRequestAsync(request);

                // Send the request with AWS credentials
                var httpResponse = await _healthLakeHttpService.SendSignedRequestAsync(clonedRequest, cancellationToken);

                if (!httpResponse.IsSuccessStatusCode)
                {
                    var errorContent = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

                    // Handle 404 NotFound errors specifically
                    if (httpResponse.StatusCode == HttpStatusCode.NotFound)
                    {
                        _logger.LogWarning("Binary {BinaryId} not found (404) during memory download [CorrelationId: {CorrelationId}]",
                            binaryId, correlationId);

                        // Track the NotFound binary with full URL
                        await _notFoundTracker.TrackNotFoundBinaryAsync(
                            binaryId,
                            binaryUrl,
                            patientId,
                            documentReferenceId,
                            correlationId,
                            cancellationToken);
                    }

                    throw new DocumentDownloadException(
                        $"Failed to download Binary {binaryId}: {httpResponse.StatusCode} - {errorContent}",
                        httpResponse.StatusCode == HttpStatusCode.TooManyRequests,
                        ErrorCategory.Infrastructure);
                }
                return httpResponse;
            });

            using (response)
            {
                // Read the JSON response and extract the base64 data
                var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);

                _logger.LogDebug("Parsing FHIR Binary JSON response for memory download {BinaryId} [CorrelationId: {CorrelationId}]",
                    binaryId, correlationId);

                var binaryInfo = ExtractBinaryDataFromFhirJson(jsonContent, binaryId, correlationId);
                var fileName = ExtractFileNameFromHeaders(response.Headers);

                var result = new BinaryContent
                {
                    BinaryId = binaryId,
                    Data = binaryInfo.Data, // Use decoded binary data instead of raw JSON
                    ContentType = binaryInfo.ContentType, // Use content type from FHIR Binary
                    FileName = fileName
                };

                _logger.LogInformation("Binary downloaded to memory: BinaryId={BinaryId}, Size={SizeBytes} bytes [CorrelationId: {CorrelationId}]",
                    binaryId, result.SizeBytes, correlationId);

                return result;
            }
        }
        catch (Exception ex) when (!(ex is DocumentDownloadException))
        {
            _logger.LogError(ex, "Unexpected error downloading binary {BinaryId} to memory [CorrelationId: {CorrelationId}]", binaryId, correlationId);
            throw new DocumentDownloadException($"Failed to download binary {binaryId}: {ex.Message}", ex, true, ErrorCategory.Infrastructure);
        }
    }

    public async Task<BinaryMetadata> GetBinaryMetadataAsync(
        string binaryId,
        string? patientId = null,
        string? documentReferenceId = null,
        CancellationToken cancellationToken = default)
    {
        var correlationId = _correlationIdProvider.GetCorrelationId();

        try
        {
            // Apply rate limiting
            var rateLimiter = _rateLimiterService.GetRateLimiter("HealthLake");
            await rateLimiter.WaitAsync(cancellationToken);

            // Build the Binary resource URL
            var binaryUrl = BuildBinaryUrl(binaryId);

            // Create HEAD request to get metadata only
            using var request = new HttpRequestMessage(HttpMethod.Head, binaryUrl);

            // Execute with retry policy
            var retryPolicy = _retryPolicyService.GetCombinedPolicy("HealthLake");
            var response = await retryPolicy.ExecuteAsync(async () =>
            {
                // Clone request for potential retries
                var clonedRequest = await _healthLakeHttpService.CloneHttpRequestAsync(request);

                // Send the request with AWS credentials
                var httpResponse = await _healthLakeHttpService.SendSignedRequestAsync(clonedRequest, cancellationToken);

                if (!httpResponse.IsSuccessStatusCode)
                {
                    var errorContent = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

                    // Handle 404 NotFound errors specifically
                    if (httpResponse.StatusCode == HttpStatusCode.NotFound)
                    {
                        _logger.LogWarning("Binary {BinaryId} not found (404) during metadata retrieval [CorrelationId: {CorrelationId}]",
                            binaryId, correlationId);

                        // Track the NotFound binary with full URL
                        await _notFoundTracker.TrackNotFoundBinaryAsync(
                            binaryId,
                            binaryUrl,
                            patientId,
                            documentReferenceId,
                            correlationId,
                            cancellationToken);
                    }

                    throw new DocumentDownloadException(
                        $"Failed to get Binary metadata {binaryId}: {httpResponse.StatusCode} - {errorContent}",
                        httpResponse.StatusCode == HttpStatusCode.TooManyRequests,
                        ErrorCategory.Infrastructure);
                }
                return httpResponse;
            });

            using (response)
            {
                var result = new BinaryMetadata
                {
                    BinaryId = binaryId,
                    ContentType = response.Content.Headers.ContentType?.MediaType,
                    SizeBytes = response.Content.Headers.ContentLength,
                    FileName = ExtractFileNameFromHeaders(response.Headers),
                    LastModified = response.Headers.Date?.DateTime
                };

                return result;
            }
        }
        catch (Exception ex) when (!(ex is DocumentDownloadException))
        {
            _logger.LogError(ex, "Unexpected error getting binary metadata {BinaryId} [CorrelationId: {CorrelationId}]", binaryId, correlationId);
            throw new DocumentDownloadException($"Failed to get binary metadata {binaryId}: {ex.Message}", ex, true, ErrorCategory.Infrastructure);
        }
    }

    private string BuildBinaryUrl(string binaryId)
    {
        return $"https://healthlake.{_healthLakeConfig.Region}.amazonaws.com/datastore/{_healthLakeConfig.DatastoreId}/r4/Binary/{binaryId}";
    }

    private static string? ExtractFileNameFromHeaders(System.Net.Http.Headers.HttpResponseHeaders headers)
    {
        if (headers.TryGetValues("Content-Disposition", out var values))
        {
            var contentDisposition = values.FirstOrDefault();
            if (!string.IsNullOrEmpty(contentDisposition))
            {
                // Parse filename from Content-Disposition header
                var fileNameMatch = System.Text.RegularExpressions.Regex.Match(contentDisposition, @"filename[*]?=['""]?([^'""\s]+)['""]?");
                if (fileNameMatch.Success)
                {
                    return fileNameMatch.Groups[1].Value;
                }
            }
        }

        return null;
    }

    private async Task<S3UploadResult> UploadSinglePartToS3Async(
        Stream contentStream,
        string s3Bucket,
        string s3Key,
        string? contentType,
        S3TagSet? s3TagSet,
        CancellationToken cancellationToken)
    {
        // Store the stream length before uploading (stream will be disposed after upload)
        var streamLength = contentStream.Length;

        var request = new PutObjectRequest
        {
            BucketName = s3Bucket,
            Key = s3Key,
            InputStream = contentStream,
            ContentType = contentType ?? "application/octet-stream",
            StorageClass = S3StorageClass.StandardInfrequentAccess // TODO: Make configurable
        };

        // Configure server-side encryption - TODO: Make configurable
        request.ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256;

        if (s3TagSet?.Tags.Any() == true)
        {
            request.TagSet = s3TagSet.Tags.Select(tag => new Tag { Key = tag.Key, Value = tag.Value }).ToList();
        }

        var response = await _s3Client.PutObjectAsync(request, cancellationToken);

        return new S3UploadResult
        {
            ETag = response.ETag,
            SizeBytes = streamLength,
            UsedMultipartUpload = false
        };
    }

    private async Task<S3UploadResult> UploadMultipartToS3Async(
        Stream contentStream,
        string s3Bucket,
        string s3Key,
        string? contentType,
        S3TagSet? s3TagSet,
        CancellationToken cancellationToken)
    {
        // Initiate multipart upload
        var initiateRequest = new InitiateMultipartUploadRequest
        {
            BucketName = s3Bucket,
            Key = s3Key,
            ContentType = contentType ?? "application/octet-stream",
            StorageClass = S3StorageClass.StandardInfrequentAccess // TODO: Make configurable
        };

        // Configure server-side encryption - TODO: Make configurable
        initiateRequest.ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256;

        if (s3TagSet?.Tags.Any() == true)
        {
            initiateRequest.TagSet = s3TagSet.Tags.Select(tag => new Tag { Key = tag.Key, Value = tag.Value }).ToList();
        }

        var initiateResponse = await _s3Client.InitiateMultipartUploadAsync(initiateRequest, cancellationToken);
        var uploadId = initiateResponse.UploadId;

        try
        {
            var partETags = new List<PartETag>();
            var partNumber = 1;
            var totalBytes = 0L;
            var partSize = 10 * 1024 * 1024; // 10MB - TODO: Make configurable
            var buffer = new byte[partSize];

            while (true)
            {
                var bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                if (bytesRead == 0) break;

                var partStream = new MemoryStream(buffer, 0, bytesRead);
                var uploadPartRequest = new UploadPartRequest
                {
                    BucketName = s3Bucket,
                    Key = s3Key,
                    UploadId = uploadId,
                    PartNumber = partNumber,
                    InputStream = partStream
                };

                var uploadPartResponse = await _s3Client.UploadPartAsync(uploadPartRequest, cancellationToken);
                partETags.Add(new PartETag(partNumber, uploadPartResponse.ETag));

                totalBytes += bytesRead;
                partNumber++;
            }

            // Complete multipart upload
            var completeRequest = new CompleteMultipartUploadRequest
            {
                BucketName = s3Bucket,
                Key = s3Key,
                UploadId = uploadId,
                PartETags = partETags
            };

            var completeResponse = await _s3Client.CompleteMultipartUploadAsync(completeRequest, cancellationToken);

            return new S3UploadResult
            {
                ETag = completeResponse.ETag,
                SizeBytes = totalBytes,
                UsedMultipartUpload = true
            };
        }
        catch
        {
            // Abort multipart upload on error
            try
            {
                await _s3Client.AbortMultipartUploadAsync(new AbortMultipartUploadRequest
                {
                    BucketName = s3Bucket,
                    Key = s3Key,
                    UploadId = uploadId
                }, cancellationToken);
            }
            catch (Exception abortEx)
            {
                _logger.LogWarning(abortEx, "Failed to abort multipart upload {UploadId} for key {Key}", uploadId, s3Key);
            }

            throw;
        }
    }

    private BinaryInfo ExtractBinaryDataFromFhirJson(string jsonContent, string binaryId, string correlationId)
    {
        try
        {
            using var jsonDoc = JsonDocument.Parse(jsonContent);
            var root = jsonDoc.RootElement;

            // Check if this is a FHIR Binary resource
            if (!root.TryGetProperty("resourceType", out var resourceType) ||
                resourceType.GetString() != "Binary")
            {
                throw new DocumentDownloadException(
                    $"Response is not a FHIR Binary resource for {binaryId}",
                    false, ErrorCategory.Validation);
            }

            // Extract the content type
            string? contentType = null;
            if (root.TryGetProperty("contentType", out var contentTypeProperty))
            {
                contentType = contentTypeProperty.GetString();
            }

            // Extract the base64-encoded data
            if (!root.TryGetProperty("data", out var dataProperty))
            {
                throw new DocumentDownloadException(
                    $"FHIR Binary resource {binaryId} does not contain 'data' field",
                    false, ErrorCategory.Validation);
            }

            var base64Data = dataProperty.GetString();
            if (string.IsNullOrEmpty(base64Data))
            {
                throw new DocumentDownloadException(
                    $"FHIR Binary resource {binaryId} has empty 'data' field",
                    false, ErrorCategory.Validation);
            }

            _logger.LogDebug("Decoding base64 data for binary {BinaryId}, base64 length: {Base64Length} [CorrelationId: {CorrelationId}]",
                binaryId, base64Data.Length, correlationId);

            // Decode base64 to binary data
            var binaryData = Convert.FromBase64String(base64Data);

            _logger.LogDebug("Successfully decoded binary data for {BinaryId}, decoded size: {DecodedSize} bytes, contentType: {ContentType} [CorrelationId: {CorrelationId}]",
                binaryId, binaryData.Length, contentType, correlationId);

            return new BinaryInfo
            {
                Data = binaryData,
                ContentType = contentType
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse FHIR Binary JSON for {BinaryId} [CorrelationId: {CorrelationId}]", binaryId, correlationId);
            throw new DocumentDownloadException(
                $"Invalid JSON in FHIR Binary response for {binaryId}: {ex.Message}",
                ex, false, ErrorCategory.Validation);
        }
        catch (FormatException ex)
        {
            _logger.LogError(ex, "Failed to decode base64 data for {BinaryId} [CorrelationId: {CorrelationId}]", binaryId, correlationId);
            throw new DocumentDownloadException(
                $"Invalid base64 data in FHIR Binary {binaryId}: {ex.Message}",
                ex, false, ErrorCategory.Validation);
        }
    }

    private class BinaryInfo
    {
        public required byte[] Data { get; set; }
        public string? ContentType { get; set; }
    }

    private class S3UploadResult
    {
        public string? ETag { get; set; }
        public long SizeBytes { get; set; }
        public bool UsedMultipartUpload { get; set; }
    }
}