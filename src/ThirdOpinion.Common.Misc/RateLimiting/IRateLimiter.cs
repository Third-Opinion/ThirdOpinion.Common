namespace ThirdOpinion.Common.Misc.RateLimiting;

/// <summary>
/// Interface for rate limiting service using token bucket algorithm
/// </summary>
public interface IRateLimiter
{
    /// <summary>
    /// Asynchronously waits for a token to become available
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task that completes when a token is acquired</returns>
    Task WaitAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Attempts to acquire a token without waiting
    /// </summary>
    /// <returns>True if a token was acquired, false otherwise</returns>
    bool TryAcquire();
    
    /// <summary>
    /// Attempts to acquire a token asynchronously with a timeout
    /// </summary>
    /// <param name="timeout">Maximum time to wait</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if a token was acquired within the timeout, false otherwise</returns>
    Task<bool> TryAcquireAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the current status of the rate limiter
    /// </summary>
    RateLimitStatus GetStatus();
    
    /// <summary>
    /// Gets the service name this rate limiter is for
    /// </summary>
    string ServiceName { get; }
    
    /// <summary>
    /// Gets the configured rate in calls per second
    /// </summary>
    double CallsPerSecond { get; }
}