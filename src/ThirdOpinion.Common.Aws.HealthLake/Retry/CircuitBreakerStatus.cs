namespace ThirdOpinion.Common.Aws.HealthLake.Retry;

/// <summary>
/// Circuit breaker status information
/// </summary>
public class CircuitBreakerStatus
{
    public string State { get; set; } = "Closed";
    public int FailureCount { get; set; }
    public DateTime? LastStateChange { get; set; }
}