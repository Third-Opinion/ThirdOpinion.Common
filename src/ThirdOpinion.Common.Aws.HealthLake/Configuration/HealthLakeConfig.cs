using System.ComponentModel.DataAnnotations;
using ThirdOpinion.Common.Aws.HealthLake.Retry;

namespace ThirdOpinion.Common.Aws.HealthLake.Configuration;

/// <summary>
/// Configuration settings for AWS HealthLake integration
/// </summary>
public class HealthLakeConfig
{
    /// <summary>
    /// AWS region where the HealthLake datastore is located
    /// </summary>
    [Required]
    public string Region { get; set; } = string.Empty;

    /// <summary>
    /// HealthLake datastore identifier
    /// </summary>
    [Required]
    public string DatastoreId { get; set; } = string.Empty;

    /// <summary>
    /// AWS profile name. Optional - if not provided, will use the default AWS credential chain.
    /// Can also be set via AWS_PROFILE environment variable. The AWS_ENVIRONMENT environment 
    /// variable can be used to automatically select the appropriate profile (e.g., dev, test, prod).
    /// </summary>
    public string? ProfileName { get; set; }

    /// <summary>
    /// Maximum number of requests per minute for rate limiting
    /// </summary>
    [Range(1, 10000)]
    public int RateLimitPerMinute { get; set; } = 100;

    /// <summary>
    /// Request timeout in seconds
    /// </summary>
    [Range(1, 300)]
    public int RequestTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Whether to use AWS SSO for authentication
    /// </summary>
    public bool UseSSO { get; set; } = true;
    
    /// <summary>
    /// Retry policy configuration for HealthLake API calls
    /// </summary>
    public RetryConfig? RetryPolicy { get; set; }
    
    /// <summary>
    /// Circuit breaker configuration for HealthLake API calls
    /// </summary>
    public CircuitBreakerConfig? CircuitBreaker { get; set; }

    /// <summary>
    /// S3 configuration for document storage
    /// </summary>
    public S3Config? S3 { get; set; }

    /// <summary>
    /// S3 bucket name for document storage (convenience property)
    /// </summary>
    public string S3BucketName => S3?.BucketName ?? "healthlake-documents";
}

/// <summary>
/// S3 configuration for document storage
/// </summary>
public class S3Config
{
    /// <summary>
    /// S3 bucket name where documents will be stored
    /// </summary>
    [Required]
    public string BucketName { get; set; } = string.Empty;

    /// <summary>
    /// Multipart upload threshold in bytes (default: 100MB)
    /// Files larger than this will use multipart upload
    /// </summary>
    [Range(1024 * 1024, long.MaxValue)] // Minimum 1MB
    public long MultipartUploadThreshold { get; set; } = 100 * 1024 * 1024; // 100MB

    /// <summary>
    /// Part size for multipart uploads in bytes (default: 10MB)
    /// </summary>
    [Range(5 * 1024 * 1024, long.MaxValue)] // AWS minimum 5MB per part
    public long MultipartUploadPartSize { get; set; } = 10 * 1024 * 1024; // 10MB

    /// <summary>
    /// S3 storage class to use for uploaded documents
    /// </summary>
    public string StorageClass { get; set; } = "STANDARD";

    /// <summary>
    /// Whether to enable server-side encryption
    /// </summary>
    public bool EnableServerSideEncryption { get; set; } = true;

    /// <summary>
    /// KMS key ID for server-side encryption (optional)
    /// </summary>
    public string? KmsKeyId { get; set; }
}