namespace ThirdOpinion.Common.Aws.HealthLake.Configuration;

/// <summary>
/// Circuit breaker configuration settings
/// </summary>
public class CircuitBreakerConfig
{
    public int FailureThreshold { get; set; } = 5;
    public int SuccessThreshold { get; set; } = 2;
    public TimeSpan DurationOfBreak { get; set; } = TimeSpan.FromSeconds(30);
}