namespace ThirdOpinion.Common.Misc.Retry;

/// <summary>
/// Status of a circuit breaker
/// </summary>
public enum CircuitBreakerState
{
    /// <summary>
    /// Circuit is closed and requests are flowing normally
    /// </summary>
    Closed,

    /// <summary>
    /// Circuit is open and requests are being rejected
    /// </summary>
    Open,

    /// <summary>
    /// Circuit is half-open and testing if the service has recovered
    /// </summary>
    HalfOpen
}

/// <summary>
/// Detailed status information about a circuit breaker
/// </summary>
public class CircuitBreakerStatus
{
    /// <summary>
    /// Current state of the circuit breaker
    /// </summary>
    public CircuitBreakerState State { get; set; }

    /// <summary>
    /// Service name this circuit breaker protects
    /// </summary>
    public string ServiceName { get; set; } = "";

    /// <summary>
    /// Number of consecutive failures
    /// </summary>
    public int ConsecutiveFailures { get; set; }

    /// <summary>
    /// Total number of failures in the current sampling window
    /// </summary>
    public int FailuresInSamplingPeriod { get; set; }

    /// <summary>
    /// Total number of requests in the current sampling window
    /// </summary>
    public int RequestsInSamplingPeriod { get; set; }

    /// <summary>
    /// Current failure rate (0.0 to 1.0)
    /// </summary>
    public double FailureRate => RequestsInSamplingPeriod > 0 
        ? (double)FailuresInSamplingPeriod / RequestsInSamplingPeriod 
        : 0.0;

    /// <summary>
    /// When the circuit was last opened (null if never opened)
    /// </summary>
    public DateTimeOffset? LastOpenedAt { get; set; }

    /// <summary>
    /// When the circuit will next allow a test request (if half-open)
    /// </summary>
    public DateTimeOffset? NextTestAt { get; set; }

    /// <summary>
    /// Duration the circuit has been in current state
    /// </summary>
    public TimeSpan TimeInCurrentState => LastOpenedAt.HasValue 
        ? DateTimeOffset.UtcNow - LastOpenedAt.Value 
        : TimeSpan.Zero;

    /// <summary>
    /// Whether the circuit breaker is healthy (closed state)
    /// </summary>
    public bool IsHealthy => State == CircuitBreakerState.Closed;
}