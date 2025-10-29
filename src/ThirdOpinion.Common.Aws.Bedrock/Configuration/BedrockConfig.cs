namespace ThirdOpinion.Common.Aws.Bedrock.Configuration;

/// <summary>
/// Configuration for AWS Bedrock service
/// </summary>
public class BedrockConfig
{
    /// <summary>
    /// AWS region for Bedrock service
    /// </summary>
    public string Region { get; set; } = "us-east-1";

    /// <summary>
    /// Maximum number of retries for API calls
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Timeout in seconds for API calls
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Default model ID to use if none specified
    /// </summary>
    public string? DefaultModelId { get; set; }

    /// <summary>
    /// Default maximum tokens for completions
    /// </summary>
    public int DefaultMaxTokens { get; set; } = 1024;

    /// <summary>
    /// Default temperature for model responses
    /// </summary>
    public double DefaultTemperature { get; set; } = 0.7;

    /// <summary>
    /// Enable debug logging
    /// </summary>
    public bool EnableDebugLogging { get; set; } = false;
}