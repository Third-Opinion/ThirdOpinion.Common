using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace ThirdOpinion.Common.Misc.RateLimiting;

/// <summary>
/// Generic service for managing rate limiters for different API services
/// </summary>
public class GenericRateLimiterService : IRateLimiterService, IDisposable
{
    private readonly ConcurrentDictionary<string, IRateLimiter> _rateLimiters = new();
    private readonly ILogger<GenericRateLimiterService> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly RateLimitMetrics _metrics = new();

    public GenericRateLimiterService(
        ILogger<GenericRateLimiterService> logger,
        ILoggerFactory loggerFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    /// <summary>
    /// Register a rate limiter for a service
    /// </summary>
    public void RegisterRateLimiter(string serviceName, int requestsPerMinute, bool useAdaptive = false)
    {
        var config = RateLimitConfig.FromPerMinute(serviceName, requestsPerMinute);
        var serviceLogger = _loggerFactory.CreateLogger<TokenBucketRateLimiter>();
        var metrics = _metrics.GetMetrics(serviceName);

        var tokenBucket = new TokenBucketRateLimiter(config, serviceLogger, metrics);
        IRateLimiter rateLimiter = useAdaptive
            ? new AdaptiveRateLimiter(tokenBucket, _loggerFactory.CreateLogger<AdaptiveRateLimiter>())
            : tokenBucket;

        _rateLimiters[serviceName] = rateLimiter;

        _logger.LogInformation("Registered rate limiter for {ServiceName} with {RequestsPerMinute} requests/minute",
            serviceName, requestsPerMinute);
    }

    /// <summary>
    /// Update rate limit for a service
    /// </summary>
    public void UpdateRateLimit(string serviceName, int newRequestsPerMinute)
    {
        var newRate = newRequestsPerMinute / 60.0;
        UpdateRateLimit(serviceName, newRate);
    }

    /// <summary>
    /// Updates the rate limit for a service at runtime
    /// </summary>
    public void UpdateRateLimit(string serviceName, double callsPerSecond)
    {
        _logger.LogInformation(
            "Updating rate limit for {ServiceName} to {CallsPerSecond:F2} calls/sec",
            serviceName, callsPerSecond);

        // Dispose existing rate limiter if present
        if (_rateLimiters.TryRemove(serviceName, out var existingLimiter))
        {
            if (existingLimiter is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        // Create new rate limiter if rate is positive
        if (callsPerSecond > 0)
        {
            var config = new RateLimitConfig
            {
                ServiceName = serviceName,
                CallsPerSecond = callsPerSecond,
                Enabled = true
            };

            var logger = _loggerFactory.CreateLogger<TokenBucketRateLimiter>();
            var metrics = _metrics.GetMetrics(serviceName);
            _rateLimiters[serviceName] = new TokenBucketRateLimiter(config, logger, metrics);
        }
    }

    /// <summary>
    /// Gets a rate limiter for the specified service
    /// </summary>
    public IRateLimiter GetRateLimiter(string serviceName)
    {
        if (_rateLimiters.TryGetValue(serviceName, out var rateLimiter))
        {
            return rateLimiter;
        }

        // Return a no-op rate limiter if not configured
        _logger.LogDebug("No rate limiter configured for service: {ServiceName}", serviceName);
        return new NoOpRateLimiter(serviceName);
    }

    /// <summary>
    /// Gets all configured rate limiters
    /// </summary>
    public IEnumerable<IRateLimiter> GetAllRateLimiters()
    {
        return _rateLimiters.Values;
    }

    public async Task<bool> TryAcquireAsync(string serviceName, int tokens = 1, CancellationToken cancellationToken = default)
    {
        if (_rateLimiters.TryGetValue(serviceName, out var rateLimiter))
        {
            // Use TryAcquireAsync with a reasonable timeout
            return await rateLimiter.TryAcquireAsync(TimeSpan.FromSeconds(1), cancellationToken);
        }

        // If no rate limiter exists for the service, allow the request
        _logger.LogDebug("No rate limiter configured for service {ServiceName}, allowing request", serviceName);
        return true;
    }

    public bool TryAcquire(string serviceName, int tokens = 1)
    {
        if (_rateLimiters.TryGetValue(serviceName, out var rateLimiter))
        {
            return rateLimiter.TryAcquire();
        }

        // If no rate limiter exists for the service, allow the request
        _logger.LogDebug("No rate limiter configured for service {ServiceName}, allowing request", serviceName);
        return true;
    }

    public async Task WaitAsync(string serviceName, int tokens = 1, CancellationToken cancellationToken = default)
    {
        if (_rateLimiters.TryGetValue(serviceName, out var rateLimiter))
        {
            await rateLimiter.WaitAsync(cancellationToken);
        }
        // If no rate limiter exists, don't wait
    }

    public int GetAvailableTokens(string serviceName)
    {
        if (_rateLimiters.TryGetValue(serviceName, out var rateLimiter))
        {
            return rateLimiter.GetStatus().AvailableTokens;
        }

        return int.MaxValue; // No limit
    }

    public double GetCurrentRate(string serviceName)
    {
        if (_rateLimiters.TryGetValue(serviceName, out var rateLimiter))
        {
            return rateLimiter.CallsPerSecond;
        }

        return double.MaxValue; // No limit
    }

    public RateLimitStatus GetStatus(string serviceName)
    {
        if (_rateLimiters.TryGetValue(serviceName, out var rateLimiter))
        {
            return rateLimiter.GetStatus();
        }

        return new RateLimitStatus
        {
            ServiceName = serviceName,
            AvailableTokens = int.MaxValue,
            MaxTokens = int.MaxValue,
            CurrentRate = double.MaxValue,
            NextRefillTime = DateTime.UtcNow,
            WaitingRequests = 0
        };
    }

    public Dictionary<string, RateLimitStatus> GetAllStatuses()
    {
        var statuses = new Dictionary<string, RateLimitStatus>();

        foreach (var kvp in _rateLimiters)
        {
            statuses[kvp.Key] = kvp.Value.GetStatus();
        }

        return statuses;
    }

    public RateLimitMetrics GetMetrics()
    {
        return _metrics;
    }

    public ServiceMetrics GetMetrics(string serviceName)
    {
        return _metrics.GetMetrics(serviceName);
    }

    public void Dispose()
    {
        foreach (var rateLimiter in _rateLimiters.Values)
        {
            if (rateLimiter is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        _rateLimiters.Clear();
    }

    /// <summary>
    /// No-op rate limiter for services without rate limiting
    /// </summary>
    private class NoOpRateLimiter : IRateLimiter
    {
        public string ServiceName { get; }
        public double CallsPerSecond => double.MaxValue;

        public NoOpRateLimiter(string serviceName)
        {
            ServiceName = serviceName;
        }

        public Task WaitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task WaitAsync(int tokens = 1, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public bool TryAcquire() => true;
        public Task<bool> TryAcquireAsync(TimeSpan timeout, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public RateLimitStatus GetStatus() => new()
        {
            ServiceName = ServiceName,
            AvailableTokens = int.MaxValue,
            MaxTokens = int.MaxValue,
            CurrentRate = CallsPerSecond,
            NextRefillTime = DateTime.UtcNow.AddYears(1),
            WaitingRequests = 0
        };
    }
}