using System.ComponentModel.DataAnnotations;

namespace ThirdOpinion.Common.IA.Pipelines.Services.S3;

/// <summary>
/// Configuration options for <see cref="S3ArtifactStorageService"/>.
/// </summary>
public class S3ArtifactStorageOptions
{
    /// <summary>
    /// Name of the bucket where artifacts are stored.
    /// </summary>
    [Required]
    public string BucketName { get; set; } = string.Empty;

    /// <summary>
    /// Optional prefix applied to every S3 key.
    /// Trailing slashes are trimmed automatically.
    /// </summary>
    public string? KeyPrefix { get; set; } = "artifacts";

    /// <summary>
    /// Whether to ensure the target bucket exists before uploading.
    /// </summary>
    public bool EnsureBucketExists { get; set; } = true;

    /// <summary>
    /// Maximum number of concurrent uploads performed inside a batch.
    /// </summary>
    [Range(1, 64)]
    public int MaxConcurrentUploads { get; set; } = 8;
}


