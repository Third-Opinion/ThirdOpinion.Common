using Polly;

namespace ThirdOpinion.Common.Misc.Retry;

/// <summary>
///     Service for managing retry policies for different services
/// </summary>
public interface IRetryPolicyService
{
    /// <summary>
    ///     Gets the retry policy for a specific service
    /// </summary>
    /// <param name="serviceName">Name of the service (e.g., "Athena", "HealthLake")</param>
    /// <returns>The configured retry policy for the service</returns>
    IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(string serviceName);

    /// <summary>
    ///     Gets the circuit breaker policy for a specific service
    /// </summary>
    /// <param name="serviceName">Name of the service</param>
    /// <returns>The configured circuit breaker policy for the service</returns>
    IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy(string serviceName);

    /// <summary>
    ///     Gets a combined retry and circuit breaker policy for a service
    /// </summary>
    /// <param name="serviceName">Name of the service</param>
    /// <returns>Combined policy with retry and circuit breaker</returns>
    IAsyncPolicy<HttpResponseMessage> GetCombinedPolicy(string serviceName);

    /// <summary>
    ///     Gets the current status of the circuit breaker for a service
    /// </summary>
    /// <param name="serviceName">Name of the service</param>
    /// <returns>Current circuit breaker status</returns>
    CircuitBreakerStatus GetCircuitBreakerStatus(string serviceName);

    /// <summary>
    ///     Gets retry metrics for a service
    /// </summary>
    /// <param name="serviceName">Name of the service</param>
    /// <returns>Retry metrics including attempt counts and failure rates</returns>
    RetryMetrics GetRetryMetrics(string serviceName);

    /// <summary>
    ///     Resets the circuit breaker for a service (manual intervention)
    /// </summary>
    /// <param name="serviceName">Name of the service</param>
    void ResetCircuitBreaker(string serviceName);
}