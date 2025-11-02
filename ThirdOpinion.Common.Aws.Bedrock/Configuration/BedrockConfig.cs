namespace ThirdOpinion.Common.Aws.Bedrock.Configuration;

/// <summary>
///     Configuration for AWS Bedrock service
/// </summary>
public class BedrockConfig
{
    /// <summary>
    ///     AWS region for Bedrock service
    /// </summary>
    public string Region { get; set; } = "us-east-1";

    /// <summary>
    ///     Maximum number of retries for API calls
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    ///     Timeout in seconds for API calls
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    ///     Request timeout in seconds (alias for TimeoutSeconds)
    /// </summary>
    public int RequestTimeoutSeconds
    {
        get => TimeoutSeconds;
        set => TimeoutSeconds = value;
    }

    /// <summary>
    ///     Default model ID to use if none specified
    /// </summary>
    public string? DefaultModelId { get; set; }

    /// <summary>
    ///     Default maximum tokens for completions
    /// </summary>
    public int DefaultMaxTokens { get; set; } = 1024;

    /// <summary>
    ///     Default temperature for model responses
    /// </summary>
    public double DefaultTemperature { get; set; } = 0.7;

    /// <summary>
    ///     Enable debug logging
    /// </summary>
    public bool EnableDebugLogging { get; set; } = false;

    /// <summary>
    ///     Rate limit for Bedrock API calls per minute
    /// </summary>
    public int RateLimitPerMinute { get; set; } = 100;

    /// <summary>
    ///     Model-specific configurations
    /// </summary>
    public Dictionary<string, ModelConfig>? ModelConfigurations { get; set; }

    /// <summary>
    ///     Whether the service is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Validates that the configuration is properly set up
    /// </summary>
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Region) &&
        DefaultMaxTokens > 0 &&
        DefaultTemperature >= 0.0 && DefaultTemperature <= 1.0 &&
        TimeoutSeconds > 0 &&
        MaxRetries >= 0 &&
        RateLimitPerMinute > 0;
}

/// <summary>
///     Configuration for specific models
/// </summary>
public class ModelConfig
{
    /// <summary>
    ///     The actual model ID to use with Bedrock (e.g., "anthropic.claude-opus-4-1-20250805-v1:0")
    /// </summary>
    public string? ModelId { get; set; }

    /// <summary>
    ///     Human-readable display name for this model
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    ///     Maximum tokens for this model
    /// </summary>
    public int? MaxTokens { get; set; }

    /// <summary>
    ///     Default temperature for this model
    /// </summary>
    public double? Temperature { get; set; }

    /// <summary>
    ///     Whether this model is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Custom rate limit for this model
    /// </summary>
    public int? RateLimitPerMinute { get; set; }

    /// <summary>
    ///     Cost per 1000 input tokens (for monitoring)
    /// </summary>
    public decimal? CostPer1KInputTokens { get; set; }

    /// <summary>
    ///     Cost per 1000 output tokens (for monitoring)
    /// </summary>
    public decimal? CostPer1KOutputTokens { get; set; }

    /// <summary>
    ///     Search terms for document processing (from Langfuse configuration)
    /// </summary>
    public string? SearchTermsDocument { get; set; }
}