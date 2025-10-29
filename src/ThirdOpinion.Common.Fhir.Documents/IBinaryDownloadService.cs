using ThirdOpinion.Common.Fhir.Documents.Models;

namespace ThirdOpinion.Common.Fhir.Documents;

/// <summary>
/// Service for downloading Binary resources from HealthLake with streaming support
/// </summary>
public interface IBinaryDownloadService
{
    /// <summary>
    /// Downloads a Binary resource and streams it directly to S3
    /// </summary>
    /// <param name="binaryId">The Binary resource ID</param>
    /// <param name="s3Bucket">The S3 bucket name</param>
    /// <param name="s3Key">The S3 key where the binary will be stored</param>
    /// <param name="s3TagSet">Optional S3 tags to apply to the object</param>
    /// <param name="patientId">Optional patient ID for NotFound tracking</param>
    /// <param name="documentReferenceId">Optional DocumentReference ID for NotFound tracking</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Information about the downloaded binary</returns>
    Task<BinaryDownloadResult> DownloadBinaryToS3Async(
        string binaryId,
        string s3Bucket,
        string s3Key,
        S3TagSet? s3TagSet = null,
        string? patientId = null,
        string? documentReferenceId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a Binary resource to memory (for smaller files)
    /// </summary>
    /// <param name="binaryId">The Binary resource ID</param>
    /// <param name="patientId">Optional patient ID for NotFound tracking</param>
    /// <param name="documentReferenceId">Optional DocumentReference ID for NotFound tracking</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The binary content and metadata</returns>
    Task<BinaryContent> DownloadBinaryToMemoryAsync(
        string binaryId,
        string? patientId = null,
        string? documentReferenceId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets metadata about a Binary resource without downloading the content
    /// </summary>
    /// <param name="binaryId">The Binary resource ID</param>
    /// <param name="patientId">Optional patient ID for NotFound tracking</param>
    /// <param name="documentReferenceId">Optional DocumentReference ID for NotFound tracking</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Binary metadata</returns>
    Task<BinaryMetadata> GetBinaryMetadataAsync(
        string binaryId,
        string? patientId = null,
        string? documentReferenceId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a binary download operation
/// </summary>
public class BinaryDownloadResult
{
    /// <summary>
    /// The Binary resource ID
    /// </summary>
    public required string BinaryId { get; set; }

    /// <summary>
    /// The S3 key where the binary was stored
    /// </summary>
    public required string S3Key { get; set; }

    /// <summary>
    /// Size of the downloaded binary in bytes
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// Content type of the binary
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// Original filename if available
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// Time taken to download and upload
    /// </summary>
    public TimeSpan DownloadDuration { get; set; }

    /// <summary>
    /// S3 ETag of the uploaded object
    /// </summary>
    public string? S3ETag { get; set; }

    /// <summary>
    /// Whether multipart upload was used
    /// </summary>
    public bool UsedMultipartUpload { get; set; }
}

/// <summary>
/// Binary content downloaded to memory
/// </summary>
public class BinaryContent
{
    /// <summary>
    /// The Binary resource ID
    /// </summary>
    public required string BinaryId { get; set; }

    /// <summary>
    /// The binary data
    /// </summary>
    public required byte[] Data { get; set; }

    /// <summary>
    /// Content type of the binary
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// Original filename if available
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// Size of the binary in bytes
    /// </summary>
    public long SizeBytes => Data.Length;
}

/// <summary>
/// Metadata about a Binary resource
/// </summary>
public class BinaryMetadata
{
    /// <summary>
    /// The Binary resource ID
    /// </summary>
    public required string BinaryId { get; set; }

    /// <summary>
    /// Content type of the binary
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// Size of the binary in bytes (if available)
    /// </summary>
    public long? SizeBytes { get; set; }

    /// <summary>
    /// Original filename if available
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// Last modified timestamp
    /// </summary>
    public DateTime? LastModified { get; set; }
}