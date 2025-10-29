using System.ComponentModel.DataAnnotations;

namespace ThirdOpinion.Common.Langfuse.Configuration;

/// <summary>
/// Configuration settings for Langfuse API integration
/// </summary>
public class LangfuseConfiguration
{
    /// <summary>
    /// Base URL for the Langfuse API
    /// </summary>
    [Required]
    public string BaseUrl { get; set; } = "https://cloud.langfuse.com";

    /// <summary>
    /// Public key for Langfuse API authentication
    /// </summary>
    [Required]
    public string PublicKey { get; set; } = string.Empty;

    /// <summary>
    /// Secret key for Langfuse API authentication
    /// </summary>
    [Required]
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// HTTP request timeout in seconds
    /// </summary>
    [Range(1, 300)]
    public int RequestTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum number of retries for failed requests
    /// </summary>
    [Range(0, 10)]
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Enable batch mode for sending telemetry
    /// </summary>
    public bool EnableBatchMode { get; set; } = false;

    /// <summary>
    /// Batch size for telemetry ingestion
    /// </summary>
    [Range(1, 1000)]
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Flush interval in seconds for batch mode
    /// </summary>
    [Range(1, 300)]
    public int FlushIntervalSeconds { get; set; } = 10;

    /// <summary>
    /// Enable sensitive data masking in traces
    /// </summary>
    public bool EnableDataMasking { get; set; } = true;

    /// <summary>
    /// Enable debug logging
    /// </summary>
    public bool EnableDebugLogging { get; set; } = false;

    /// <summary>
    /// Environment name for telemetry tagging
    /// </summary>
    public string Environment { get; set; } = "production";

    /// <summary>
    /// Enable telemetry collection
    /// </summary>
    public bool EnableTelemetry { get; set; } = true;

    /// <summary>
    /// Indicates whether Langfuse is properly configured
    /// </summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(BaseUrl) &&
        !string.IsNullOrWhiteSpace(PublicKey) &&
        !string.IsNullOrWhiteSpace(SecretKey);
}