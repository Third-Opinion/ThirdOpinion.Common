using Amazon.S3.Model;

namespace ThirdOpinion.Common.Aws.S3;

/// <summary>
///     Interface for S3 storage operations
/// </summary>
public interface IS3Storage
{
    /// <summary>
    ///     Upload an object to S3
    /// </summary>
    Task<PutObjectResponse> PutObjectAsync(string bucketName,
        string key,
        Stream content,
        string? contentType = null,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Upload an object from string content
    /// </summary>
    Task<PutObjectResponse> PutObjectAsync(string bucketName,
        string key,
        string content,
        string? contentType = null,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Download an object from S3
    /// </summary>
    Task<Stream> GetObjectAsync(string bucketName,
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Download an object as string
    /// </summary>
    Task<string> GetObjectAsStringAsync(string bucketName,
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get object metadata without downloading content
    /// </summary>
    Task<GetObjectMetadataResponse> GetObjectMetadataAsync(string bucketName,
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Delete an object
    /// </summary>
    Task<DeleteObjectResponse> DeleteObjectAsync(string bucketName,
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Delete multiple objects
    /// </summary>
    Task<DeleteObjectsResponse> DeleteObjectsAsync(string bucketName,
        IEnumerable<string> keys,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Check if an object exists
    /// </summary>
    Task<bool> ObjectExistsAsync(string bucketName,
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     List objects with prefix
    /// </summary>
    Task<IEnumerable<S3Object>> ListObjectsAsync(string bucketName,
        string? prefix = null,
        int maxKeys = 1000,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Copy an object
    /// </summary>
    Task<CopyObjectResponse> CopyObjectAsync(string sourceBucket,
        string sourceKey,
        string destinationBucket,
        string destinationKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Generate a presigned URL for GET
    /// </summary>
    Task<string> GeneratePresignedUrlAsync(string bucketName,
        string key,
        TimeSpan expiration,
        Dictionary<string, string>? headers = null);

    /// <summary>
    ///     Generate a presigned URL for PUT
    /// </summary>
    Task<string> GeneratePresignedPutUrlAsync(string bucketName,
        string key,
        TimeSpan expiration,
        string? contentType = null,
        Dictionary<string, string>? metadata = null);

    /// <summary>
    ///     Create a bucket if it doesn't exist
    /// </summary>
    Task<bool> CreateBucketIfNotExistsAsync(string bucketName,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Initiate multipart upload
    /// </summary>
    Task<InitiateMultipartUploadResponse> InitiateMultipartUploadAsync(string bucketName,
        string key,
        string? contentType = null,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Upload part for multipart upload
    /// </summary>
    Task<UploadPartResponse> UploadPartAsync(string bucketName,
        string key,
        string uploadId,
        int partNumber,
        Stream content,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Complete multipart upload
    /// </summary>
    Task<CompleteMultipartUploadResponse> CompleteMultipartUploadAsync(string bucketName,
        string key,
        string uploadId,
        List<PartETag> parts,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Abort multipart upload
    /// </summary>
    Task AbortMultipartUploadAsync(string bucketName,
        string key,
        string uploadId,
        CancellationToken cancellationToken = default);
}