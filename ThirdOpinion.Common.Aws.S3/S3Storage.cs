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

    public S3Storage(IAmazonS3 s3Client, ILogger<S3Storage> logger)
    {
        _s3Client = s3Client;
        _logger = logger;
    }

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

    public async Task<string> GetObjectAsStringAsync(string bucketName,
        string key,
        CancellationToken cancellationToken = default)
    {
        using Stream stream = await GetObjectAsync(bucketName, key, cancellationToken);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken);
    }

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

    public async Task<bool> ObjectExistsAsync(string bucketName,
        string key,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await GetObjectMetadataAsync(bucketName, key, cancellationToken);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

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
            } while (response.IsTruncated);

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