namespace ThirdOpinion.Common.Misc.Retry;

/// <summary>
/// Configuration for retry policies
/// </summary>
public class RetryConfig
{
    /// <summary>
    /// Maximum number of retry attempts
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Initial delay for exponential backoff in milliseconds
    /// </summary>
    public int InitialDelayMs { get; set; } = 1000;

    /// <summary>
    /// Maximum delay for exponential backoff in milliseconds
    /// </summary>
    public int MaxDelayMs { get; set; } = 30000;

    /// <summary>
    /// Jitter percentage to add randomness (0.0 to 1.0)
    /// </summary>
    public double JitterPercentage { get; set; } = 0.2;

    /// <summary>
    /// Whether to enable exponential backoff
    /// </summary>
    public bool UseExponentialBackoff { get; set; } = true;

    /// <summary>
    /// HTTP status codes that should trigger a retry
    /// </summary>
    public HashSet<int> RetriableStatusCodes { get; set; } = new()
    {
        429, // Too Many Requests
        503, // Service Unavailable
        504, // Gateway Timeout
        502, // Bad Gateway
        500  // Internal Server Error (in some cases)
    };

    /// <summary>
    /// Exception types that should trigger a retry
    /// </summary>
    public HashSet<string> RetriableExceptionTypes { get; set; } = new()
    {
        "HttpRequestException",
        "TaskCanceledException", // Timeout
        "SocketException",
        "TimeoutException"
    };
}

/// <summary>
/// Configuration for circuit breaker policy
/// </summary>
public class CircuitBreakerConfig
{
    /// <summary>
    /// Number of consecutive failures before opening the circuit
    /// </summary>
    public int FailureThreshold { get; set; } = 5;

    /// <summary>
    /// Time period to monitor for failures in seconds
    /// </summary>
    public int SamplingDurationSeconds { get; set; } = 60;

    /// <summary>
    /// Minimum number of requests before circuit breaker can activate
    /// </summary>
    public int MinimumThroughput { get; set; } = 10;

    /// <summary>
    /// Duration to keep circuit open in seconds
    /// </summary>
    public int DurationOfBreakSeconds { get; set; } = 30;

    /// <summary>
    /// Whether circuit breaker is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Combined retry and circuit breaker configuration for a service
/// </summary>
public class ServiceRetryConfig
{
    /// <summary>
    /// Service name (e.g., "Athena", "HealthLake")
    /// </summary>
    public string ServiceName { get; set; } = "";

    /// <summary>
    /// Retry policy configuration
    /// </summary>
    public RetryConfig Retry { get; set; } = new();

    /// <summary>
    /// Circuit breaker configuration
    /// </summary>
    public CircuitBreakerConfig CircuitBreaker { get; set; } = new();

    /// <summary>
    /// Whether retry policies are enabled for this service
    /// </summary>
    public bool Enabled { get; set; } = true;
}