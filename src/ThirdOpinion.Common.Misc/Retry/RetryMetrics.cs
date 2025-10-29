namespace ThirdOpinion.Common.Misc.Retry;

/// <summary>
/// Metrics for retry operations
/// </summary>
public class RetryMetrics
{
    /// <summary>
    /// Service name these metrics apply to
    /// </summary>
    public string ServiceName { get; set; } = "";

    /// <summary>
    /// Total number of requests made
    /// </summary>
    public long TotalRequests { get; set; }

    /// <summary>
    /// Total number of successful requests (no retries needed)
    /// </summary>
    public long SuccessfulRequests { get; set; }

    /// <summary>
    /// Total number of requests that required retries
    /// </summary>
    public long RetriedRequests { get; set; }

    /// <summary>
    /// Total number of requests that failed after all retries
    /// </summary>
    public long FailedRequests { get; set; }

    /// <summary>
    /// Total number of retry attempts made
    /// </summary>
    public long TotalRetryAttempts { get; set; }

    /// <summary>
    /// Average number of retry attempts per retried request
    /// </summary>
    public double AverageRetryAttempts => RetriedRequests > 0 
        ? (double)TotalRetryAttempts / RetriedRequests 
        : 0.0;

    /// <summary>
    /// Success rate (0.0 to 1.0)
    /// </summary>
    public double SuccessRate => TotalRequests > 0 
        ? (double)SuccessfulRequests / TotalRequests 
        : 0.0;

    /// <summary>
    /// Failure rate after retries (0.0 to 1.0)
    /// </summary>
    public double FailureRate => TotalRequests > 0 
        ? (double)FailedRequests / TotalRequests 
        : 0.0;

    /// <summary>
    /// Average delay between retries in milliseconds
    /// </summary>
    public double AverageRetryDelayMs { get; set; }

    /// <summary>
    /// Maximum delay observed between retries in milliseconds
    /// </summary>
    public double MaxRetryDelayMs { get; set; }

    /// <summary>
    /// When these metrics were last updated
    /// </summary>
    public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Breakdown by HTTP status codes
    /// </summary>
    public Dictionary<int, long> StatusCodeCounts { get; set; } = new();

    /// <summary>
    /// Breakdown by exception types
    /// </summary>
    public Dictionary<string, long> ExceptionTypeCounts { get; set; } = new();

    /// <summary>
    /// Number of requests rejected by circuit breaker
    /// </summary>
    public long CircuitBreakerRejections { get; set; }
}