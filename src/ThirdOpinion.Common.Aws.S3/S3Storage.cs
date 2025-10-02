using System.Net;
using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Microsoft.Extensions.Logging;

namespace ThirdOpinion.Common.Aws.S3;

/// <summary>
///     S3 storage service implementation
/// </summary>
public class S3Storage : IS3Storage
{
    private readonly ILogger<S3Storage> _logger;
    private readonly IAmazonS3 _s3Client;

    /// <summary>
    /// Initializes a new instance of the S3Storage class
    /// </summary>
    /// <param name="s3Client">The Amazon S3 client</param>
    /// <param name="logger">The logger instance</param>
    public S3Storage(IAmazonS3 s3Client, ILogger<S3Storage> logger)
    {
        _s3Client = s3Client;
        _logger = logger;
    }

    /// <summary>
    /// Uploads an object to S3 from a stream
    /// </summary>
    /// <param name="bucketName">The S3 bucket name</param>
    /// <param name="key">The object key (path)</param>
    /// <param name="content">The content stream to upload</param>
    /// <param name="contentType">Optional content type (defaults to application/octet-stream)</param>
    /// <param name="metadata">Optional metadata key-value pairs</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The S3 put object response</returns>
    public async Task<PutObjectResponse> PutObjectAsync(string bucketName,
        string key,
        Stream content,
        string? contentType = null,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = key,
                InputStream = content,
                ContentType = contentType ?? "application/octet-stream"
            };

            if (metadata != null)
                foreach (KeyValuePair<string, string> kvp in metadata)
                    request.Metadata.Add(kvp.Key, kvp.Value);

            PutObjectResponse? response
                = await _s3Client.PutObjectAsync(request, cancellationToken);
            _logger.LogDebug("Uploaded object to S3: {Bucket}/{Key}", bucketName, key);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading object to S3: {Bucket}/{Key}", bucketName, key);
            throw;
        }
    }

    /// <summary>
    /// Uploads an object to S3 from a string
    /// </summary>
    /// <param name="bucketName">The S3 bucket name</param>
    /// <param name="key">The object key (path)</param>
    /// <param name="content">The string content to upload</param>
    /// <param name="contentType">Optional content type (defaults to application/octet-stream)</param>
    /// <param name="metadata">Optional metadata key-value pairs</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The S3 put object response</returns>
    public async Task<PutObjectResponse> PutObjectAsync(string bucketName,
        string key,
        string content,
        string? contentType = null,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        return await PutObjectAsync(bucketName, key, stream, contentType, metadata,
            cancellationToken);
    }

    /// <summary>
    /// Downloads an object from S3 as a stream
    /// </summary>
    /// <param name="bucketName">The S3 bucket name</param>
    /// <param name="key">The object key (path)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A stream containing the object content</returns>
    public async Task<Stream> GetObjectAsync(string bucketName,
        string key,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new GetObjectRequest
            {
                BucketName = bucketName,
                Key = key
            };

            GetObjectResponse? response
                = await _s3Client.GetObjectAsync(request, cancellationToken);
            _logger.LogDebug("Downloaded object from S3: {Bucket}/{Key}", bucketName, key);
            return response.ResponseStream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading object from S3: {Bucket}/{Key}", bucketName,
                key);
            throw;
        }
    }

    /// <summary>
    /// Downloads an object from S3 as a string
    /// </summary>
    /// <param name="bucketName">The S3 bucket name</param>
    /// <param name="key">The object key (path)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The object content as a string</returns>
    public async Task<string> GetObjectAsStringAsync(string bucketName,
        string key,
        CancellationToken cancellationToken = default)
    {
        using Stream stream = await GetObjectAsync(bucketName, key, cancellationToken);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    /// <summary>
    /// Gets metadata for an object in S3
    /// </summary>
    /// <param name="bucketName">The S3 bucket name</param>
    /// <param name="key">The object key (path)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The object metadata response</returns>
    public async Task<GetObjectMetadataResponse> GetObjectMetadataAsync(string bucketName,
        string key,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new GetObjectMetadataRequest
            {
                BucketName = bucketName,
                Key = key
            };

            return await _s3Client.GetObjectMetadataAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting object metadata from S3: {Bucket}/{Key}",
                bucketName, key);
            throw;
        }
    }

    /// <summary>
    /// Deletes a single object from S3
    /// </summary>
    /// <param name="bucketName">The S3 bucket name</param>
    /// <param name="key">The object key (path) to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The delete object response</returns>
    public async Task<DeleteObjectResponse> DeleteObjectAsync(string bucketName,
        string key,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new DeleteObjectRequest
            {
                BucketName = bucketName,
                Key = key
            };

            DeleteObjectResponse? response
                = await _s3Client.DeleteObjectAsync(request, cancellationToken);
            _logger.LogDebug("Deleted object from S3: {Bucket}/{Key}", bucketName, key);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting object from S3: {Bucket}/{Key}", bucketName, key);
            throw;
        }
    }

    /// <summary>
    /// Deletes multiple objects from S3 in a batch operation
    /// </summary>
    /// <param name="bucketName">The S3 bucket name</param>
    /// <param name="keys">The object keys (paths) to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The delete objects response</returns>
    public async Task<DeleteObjectsResponse> DeleteObjectsAsync(string bucketName,
        IEnumerable<string> keys,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new DeleteObjectsRequest
            {
                BucketName = bucketName,
                Objects = keys.Select(k => new KeyVersion { Key = k }).ToList()
            };

            DeleteObjectsResponse? response
                = await _s3Client.DeleteObjectsAsync(request, cancellationToken);
            _logger.LogDebug("Deleted {Count} objects from S3 bucket {Bucket}", keys.Count(),
                bucketName);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting objects from S3 bucket {Bucket}", bucketName);
            throw;
        }
    }

    /// <summary>
    /// Checks if an object exists in S3
    /// </summary>
    /// <param name="bucketName">The S3 bucket name</param>
    /// <param name="key">The object key (path) to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the object exists, false otherwise</returns>
    public async Task<bool> ObjectExistsAsync(string bucketName,
        string key,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new GetObjectMetadataRequest
            {
                BucketName = bucketName,
                Key = key
            };

            await _s3Client.GetObjectMetadataAsync(request, cancellationToken);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    /// <summary>
    /// Lists objects in an S3 bucket with optional prefix filtering
    /// </summary>
    /// <param name="bucketName">The S3 bucket name</param>
    /// <param name="prefix">Optional prefix to filter objects</param>
    /// <param name="maxKeys">Maximum number of keys to return per request (default 1000)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>An enumerable of S3Object instances</returns>
    public async Task<IEnumerable<S3Object>> ListObjectsAsync(string bucketName,
        string? prefix = null,
        int maxKeys = 1000,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var allObjects = new List<S3Object>();
            ListObjectsV2Response response;
            var request = new ListObjectsV2Request
            {
                BucketName = bucketName,
                Prefix = prefix,
                MaxKeys = maxKeys
            };

            do
            {
                response = await _s3Client.ListObjectsV2Async(request, cancellationToken);
                allObjects.AddRange(response.S3Objects);
                request.ContinuationToken = response.NextContinuationToken;
            } while (response.IsTruncated == true);

            _logger.LogDebug("Listed {Count} objects from S3 bucket {Bucket} with prefix {Prefix}",
                allObjects.Count, bucketName, prefix);
            return allObjects;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing objects in S3 bucket {Bucket} with prefix {Prefix}",
                bucketName, prefix);
            throw;
        }
    }

    /// <summary>
    /// Copies an object from one S3 location to another
    /// </summary>
    /// <param name="sourceBucket">The source bucket name</param>
    /// <param name="sourceKey">The source object key (path)</param>
    /// <param name="destinationBucket">The destination bucket name</param>
    /// <param name="destinationKey">The destination object key (path)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The copy object response</returns>
    public async Task<CopyObjectResponse> CopyObjectAsync(string sourceBucket,
        string sourceKey,
        string destinationBucket,
        string destinationKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new CopyObjectRequest
            {
                SourceBucket = sourceBucket,
                SourceKey = sourceKey,
                DestinationBucket = destinationBucket,
                DestinationKey = destinationKey
            };

            CopyObjectResponse? response
                = await _s3Client.CopyObjectAsync(request, cancellationToken);
            _logger.LogDebug(
                "Copied object from {SourceBucket}/{SourceKey} to {DestBucket}/{DestKey}",
                sourceBucket, sourceKey, destinationBucket, destinationKey);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error copying object from {SourceBucket}/{SourceKey} to {DestBucket}/{DestKey}",
                sourceBucket, sourceKey, destinationBucket, destinationKey);
            throw;
        }
    }

    /// <summary>
    /// Generates a presigned URL for downloading an object from S3
    /// </summary>
    /// <param name="bucketName">The S3 bucket name</param>
    /// <param name="key">The object key (path)</param>
    /// <param name="expiration">How long the URL should be valid</param>
    /// <param name="headers">Optional headers to include in the request</param>
    /// <returns>A presigned URL for downloading the object</returns>
    public async Task<string> GeneratePresignedUrlAsync(string bucketName,
        string key,
        TimeSpan expiration,
        Dictionary<string, string>? headers = null)
    {
        try
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = bucketName,
                Key = key,
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.Add(expiration),
                Protocol = Protocol.HTTPS
            };

            if (headers != null)
                foreach (KeyValuePair<string, string> header in headers)
                    request.Headers[header.Key] = header.Value;

            return await _s3Client.GetPreSignedURLAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating presigned URL for {Bucket}/{Key}", bucketName,
                key);
            throw;
        }
    }

    /// <summary>
    /// Generates a presigned URL for uploading an object to S3
    /// </summary>
    /// <param name="bucketName">The S3 bucket name</param>
    /// <param name="key">The object key (path)</param>
    /// <param name="expiration">How long the URL should be valid</param>
    /// <param name="contentType">Optional content type for the upload</param>
    /// <param name="metadata">Optional metadata key-value pairs</param>
    /// <returns>A presigned URL for uploading the object</returns>
    public async Task<string> GeneratePresignedPutUrlAsync(string bucketName,
        string key,
        TimeSpan expiration,
        string? contentType = null,
        Dictionary<string, string>? metadata = null)
    {
        try
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = bucketName,
                Key = key,
                Verb = HttpVerb.PUT,
                Expires = DateTime.UtcNow.Add(expiration),
                Protocol = Protocol.HTTPS
            };

            if (!string.IsNullOrEmpty(contentType)) request.ContentType = contentType;

            if (metadata != null)
                foreach (KeyValuePair<string, string> kvp in metadata)
                    request.Metadata[$"x-amz-meta-{kvp.Key}"] = kvp.Value;

            return await _s3Client.GetPreSignedURLAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating presigned PUT URL for {Bucket}/{Key}",
                bucketName, key);
            throw;
        }
    }

    /// <summary>
    /// Creates an S3 bucket if it doesn't already exist
    /// </summary>
    /// <param name="bucketName">The S3 bucket name to create</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the bucket was created, false if it already existed</returns>
    public async Task<bool> CreateBucketIfNotExistsAsync(string bucketName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await AmazonS3Util.DoesS3BucketExistV2Async(_s3Client, bucketName))
            {
                await _s3Client.PutBucketAsync(new PutBucketRequest
                {
                    BucketName = bucketName
                }, cancellationToken);

                _logger.LogInformation("Created S3 bucket: {Bucket}", bucketName);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating S3 bucket: {Bucket}", bucketName);
            throw;
        }
    }

    /// <summary>
    /// Initiates a multipart upload for large objects
    /// </summary>
    /// <param name="bucketName">The S3 bucket name</param>
    /// <param name="key">The object key (path)</param>
    /// <param name="contentType">Optional content type (defaults to application/octet-stream)</param>
    /// <param name="metadata">Optional metadata key-value pairs</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The initiate multipart upload response containing the upload ID</returns>
    public async Task<InitiateMultipartUploadResponse> InitiateMultipartUploadAsync(
        string bucketName,
        string key,
        string? contentType = null,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new InitiateMultipartUploadRequest
            {
                BucketName = bucketName,
                Key = key,
                ContentType = contentType ?? "application/octet-stream"
            };

            if (metadata != null)
                foreach (KeyValuePair<string, string> kvp in metadata)
                    request.Metadata.Add(kvp.Key, kvp.Value);

            InitiateMultipartUploadResponse? response
                = await _s3Client.InitiateMultipartUploadAsync(request, cancellationToken);
            _logger.LogDebug("Initiated multipart upload for {Bucket}/{Key}, UploadId: {UploadId}",
                bucketName, key, response.UploadId);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating multipart upload for {Bucket}/{Key}", bucketName,
                key);
            throw;
        }
    }

    /// <summary>
    /// Uploads a part of a multipart upload
    /// </summary>
    /// <param name="bucketName">The S3 bucket name</param>
    /// <param name="key">The object key (path)</param>
    /// <param name="uploadId">The upload ID from InitiateMultipartUpload</param>
    /// <param name="partNumber">The part number (1-based)</param>
    /// <param name="content">The content stream for this part</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The upload part response containing the ETag</returns>
    public async Task<UploadPartResponse> UploadPartAsync(string bucketName,
        string key,
        string uploadId,
        int partNumber,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new UploadPartRequest
            {
                BucketName = bucketName,
                Key = key,
                UploadId = uploadId,
                PartNumber = partNumber,
                InputStream = content
            };

            UploadPartResponse? response
                = await _s3Client.UploadPartAsync(request, cancellationToken);
            _logger.LogDebug("Uploaded part {PartNumber} for {Bucket}/{Key}, UploadId: {UploadId}",
                partNumber, bucketName, key, uploadId);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error uploading part {PartNumber} for {Bucket}/{Key}, UploadId: {UploadId}",
                partNumber, bucketName, key, uploadId);
            throw;
        }
    }

    /// <summary>
    /// Completes a multipart upload by combining all uploaded parts
    /// </summary>
    /// <param name="bucketName">The S3 bucket name</param>
    /// <param name="key">The object key (path)</param>
    /// <param name="uploadId">The upload ID from InitiateMultipartUpload</param>
    /// <param name="parts">List of PartETag objects from each uploaded part</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The complete multipart upload response</returns>
    public async Task<CompleteMultipartUploadResponse> CompleteMultipartUploadAsync(
        string bucketName,
        string key,
        string uploadId,
        List<PartETag> parts,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new CompleteMultipartUploadRequest
            {
                BucketName = bucketName,
                Key = key,
                UploadId = uploadId,
                PartETags = parts
            };

            CompleteMultipartUploadResponse? response
                = await _s3Client.CompleteMultipartUploadAsync(request, cancellationToken);
            _logger.LogDebug("Completed multipart upload for {Bucket}/{Key}, UploadId: {UploadId}",
                bucketName, key, uploadId);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error completing multipart upload for {Bucket}/{Key}, UploadId: {UploadId}",
                bucketName, key, uploadId);
            throw;
        }
    }

    /// <summary>
    /// Aborts a multipart upload and cleans up any uploaded parts
    /// </summary>
    /// <param name="bucketName">The S3 bucket name</param>
    /// <param name="key">The object key (path)</param>
    /// <param name="uploadId">The upload ID from InitiateMultipartUpload</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task AbortMultipartUploadAsync(string bucketName,
        string key,
        string uploadId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new AbortMultipartUploadRequest
            {
                BucketName = bucketName,
                Key = key,
                UploadId = uploadId
            };

            await _s3Client.AbortMultipartUploadAsync(request, cancellationToken);
            _logger.LogDebug("Aborted multipart upload for {Bucket}/{Key}, UploadId: {UploadId}",
                bucketName, key, uploadId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error aborting multipart upload for {Bucket}/{Key}, UploadId: {UploadId}",
                bucketName, key, uploadId);
            throw;
        }
    }
}