namespace ThirdOpinion.Common.Aws.HealthLake.RateLimiting;

/// <summary>
///     Interface for rate limiting
/// </summary>
public interface IRateLimiter
{
    /// <summary>
    ///     Waits for rate limit availability
    /// </summary>
    Task WaitAsync(CancellationToken cancellationToken = default);
}