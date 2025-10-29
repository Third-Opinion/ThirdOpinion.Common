namespace ThirdOpinion.Common.Aws.HealthLake.Retry;

/// <summary>
/// Retry policy metrics
/// </summary>
public class RetryMetrics
{
    public long TotalRetries { get; set; }
    public long SuccessfulRetries { get; set; }
    public long FailedRetries { get; set; }
    public Dictionary<string, long> RetriesPerService { get; set; } = new();
}