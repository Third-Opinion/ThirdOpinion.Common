namespace ThirdOpinion.Common.Misc.RateLimiting;

/// <summary>
///     Service for managing multiple rate limiters for different services
/// </summary>
public interface IRateLimiterService
{
    /// <summary>
    ///     Gets a rate limiter for the specified service
    /// </summary>
    /// <param name="serviceName">Name of the service (e.g., "Athena", "HealthLake")</param>
    /// <returns>Rate limiter for the service</returns>
    IRateLimiter GetRateLimiter(string serviceName);

    /// <summary>
    ///     Gets all configured rate limiters
    /// </summary>
    IEnumerable<IRateLimiter> GetAllRateLimiters();

    /// <summary>
    ///     Updates the rate limit for a service at runtime
    /// </summary>
    /// <param name="serviceName">Service name</param>
    /// <param name="callsPerSecond">New rate in calls per second</param>
    void UpdateRateLimit(string serviceName, double callsPerSecond);

    /// <summary>
    ///     Gets the rate limiting metrics
    /// </summary>
    RateLimitMetrics GetMetrics();
}