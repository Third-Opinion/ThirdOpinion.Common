using Amazon.S3.Model;

namespace ThirdOpinion.Common.Fhir.Documents;

/// <summary>
/// Service for managing S3 storage operations including file existence checking and uploads
/// </summary>
public interface IS3StorageService
{
    /// <summary>
    /// Checks if a file exists in the specified S3 bucket and key
    /// </summary>
    /// <param name="bucket">S3 bucket name</param>
    /// <param name="key">S3 object key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the file exists, false otherwise</returns>
    /// <exception cref="ArgumentException">Thrown when bucket or key is null or whitespace</exception>
    Task<bool> CheckFileExistsAsync(string bucket, string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads a file to S3 with optional metadata and tags
    /// </summary>
    /// <param name="bucket">S3 bucket name</param>
    /// <param name="key">S3 object key</param>
    /// <param name="content">Content stream to upload</param>
    /// <param name="metadata">Optional metadata to attach to the object</param>
    /// <param name="tags">Optional tags to attach to the object</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>S3 URI of the uploaded file</returns>
    /// <exception cref="ArgumentException">Thrown when bucket or key is null or whitespace</exception>
    /// <exception cref="ArgumentNullException">Thrown when content is null</exception>
    Task<string> UploadFileAsync(
        string bucket,
        string key,
        Stream content,
        Dictionary<string, string>? metadata = null,
        List<Tag>? tags = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles force overwrite logic by checking file existence and returning whether to proceed
    /// </summary>
    /// <param name="forceMode">Whether force mode is enabled</param>
    /// <param name="bucket">S3 bucket name</param>
    /// <param name="key">S3 object key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the operation can proceed (force mode enabled or file doesn't exist), false otherwise</returns>
    Task<bool> HandleForceOverwriteAsync(bool forceMode, string bucket, string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets detailed information about an S3 file
    /// </summary>
    /// <param name="bucket">S3 bucket name</param>
    /// <param name="key">S3 object key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>File information if the file exists, null otherwise</returns>
    /// <exception cref="ArgumentException">Thrown when bucket or key is null or whitespace</exception>
    Task<S3FileInfo?> GetFileInfoAsync(string bucket, string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a file from S3
    /// </summary>
    /// <param name="bucket">S3 bucket name</param>
    /// <param name="key">S3 object key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the file was successfully deleted, false otherwise</returns>
    /// <exception cref="ArgumentException">Thrown when bucket or key is null or whitespace</exception>
    Task<bool> DeleteFileAsync(string bucket, string key, CancellationToken cancellationToken = default);
}