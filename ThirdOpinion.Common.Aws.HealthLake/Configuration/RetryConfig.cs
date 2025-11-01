namespace ThirdOpinion.Common.Aws.HealthLake.Configuration;

/// <summary>
///     Retry configuration settings
/// </summary>
public class RetryConfig
{
    public int MaxRetries { get; set; } = 3;
    public int BaseDelayMs { get; set; } = 1000;
    public int MaxDelayMs { get; set; } = 30000;
    public bool UseExponentialBackoff { get; set; } = true;
}