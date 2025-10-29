namespace ThirdOpinion.Common.Misc.RateLimiting;

/// <summary>
/// Represents the current status of a rate limiter
/// </summary>
public class RateLimitStatus
{
    /// <summary>
    /// Number of tokens currently available
    /// </summary>
    public int AvailableTokens { get; init; }
    
    /// <summary>
    /// Maximum number of tokens (burst capacity)
    /// </summary>
    public int MaxTokens { get; init; }
    
    /// <summary>
    /// Next time tokens will be replenished
    /// </summary>
    public DateTime NextRefillTime { get; init; }
    
    /// <summary>
    /// Current rate in calls per second
    /// </summary>
    public double CurrentRate { get; init; }
    
    /// <summary>
    /// Service name this status is for
    /// </summary>
    public string ServiceName { get; init; } = string.Empty;
    
    /// <summary>
    /// Number of requests currently waiting for tokens
    /// </summary>
    public int WaitingRequests { get; init; }
    
    /// <summary>
    /// Indicates if the rate limiter is currently throttling requests
    /// </summary>
    public bool IsThrottling => AvailableTokens == 0 && WaitingRequests > 0;
    
    /// <summary>
    /// Percentage of available capacity (0-100)
    /// </summary>
    public double AvailableCapacityPercentage => MaxTokens > 0 
        ? (double)AvailableTokens / MaxTokens * 100 
        : 0;
}