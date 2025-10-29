namespace ThirdOpinion.Common.Aws.HealthLake.RateLimiting;

/// <summary>
/// Rate limiting metrics
/// </summary>
public class RateLimitMetrics
{
    public long TotalRequests { get; set; }
    public long ThrottledRequests { get; set; }
    public Dictionary<string, long> RequestsPerService { get; set; } = new();
}