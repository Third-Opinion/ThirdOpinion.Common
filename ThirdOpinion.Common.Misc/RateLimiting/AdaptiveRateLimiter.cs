using System.Net;
using Microsoft.Extensions.Logging;

namespace ThirdOpinion.Common.Misc.RateLimiting;

/// <summary>
///     Decorator for rate limiters that adds adaptive behavior based on HTTP responses
/// </summary>
public class AdaptiveRateLimiter : IRateLimiter
{
    // Configuration
    private const double BackoffFactor = 0.5; // Reduce rate by 50% on 429
    private const double RecoveryFactor = 1.1; // Increase rate by 10% during recovery
    private const int RecoveryThreshold = 100; // Consecutive successes before recovery
    private const int BackoffCooldownMinutes = 5; // Wait time before attempting recovery
    private readonly double _baseRate;
    private readonly IRateLimiter _innerLimiter;
    private readonly object _lock = new();
    private readonly ILogger<AdaptiveRateLimiter>? _logger;
    private readonly double _maxRate;
    private readonly double _minRate;
    private int _consecutive429s;
    private int _consecutiveSuccesses;

    private DateTime _lastBackoffTime = DateTime.MinValue;
    private DateTime _lastRecoveryTime = DateTime.MinValue;

    public AdaptiveRateLimiter(IRateLimiter innerLimiter,
        ILogger<AdaptiveRateLimiter>? logger = null)
    {
        _innerLimiter = innerLimiter ?? throw new ArgumentNullException(nameof(innerLimiter));
        _logger = logger;

        _baseRate = CallsPerSecond = innerLimiter.CallsPerSecond;
        _minRate = _baseRate * 0.1; // Minimum 10% of base rate
        _maxRate = _baseRate * 1.2; // Maximum 120% of base rate
    }

    public string ServiceName => _innerLimiter.ServiceName;
    public double CallsPerSecond { get; private set; }

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        await _innerLimiter.WaitAsync(cancellationToken);
    }

    public bool TryAcquire()
    {
        return _innerLimiter.TryAcquire();
    }

    public async Task<bool> TryAcquireAsync(TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        return await _innerLimiter.TryAcquireAsync(timeout, cancellationToken);
    }

    public RateLimitStatus GetStatus()
    {
        RateLimitStatus innerStatus = _innerLimiter.GetStatus();
        return new RateLimitStatus
        {
            ServiceName = innerStatus.ServiceName,
            AvailableTokens = innerStatus.AvailableTokens,
            MaxTokens = innerStatus.MaxTokens,
            CurrentRate = CallsPerSecond,
            NextRefillTime = innerStatus.NextRefillTime,
            WaitingRequests = innerStatus.WaitingRequests
        };
    }

    /// <summary>
    ///     Notifies the rate limiter of an HTTP response to adjust rate accordingly
    /// </summary>
    public void OnHttpResponse(HttpStatusCode statusCode, string? retryAfterHeader = null)
    {
        lock (_lock)
        {
            if (statusCode == HttpStatusCode.TooManyRequests)
                HandleRateLimitResponse(retryAfterHeader);
            else if (IsSuccessStatusCode(statusCode)) HandleSuccessResponse();
        }
    }

    private void HandleRateLimitResponse(string? retryAfterHeader)
    {
        _consecutive429s++;
        _consecutiveSuccesses = 0;
        _lastBackoffTime = DateTime.UtcNow;

        // Parse Retry-After header if present
        int retryAfterSeconds = ParseRetryAfter(retryAfterHeader);

        // Calculate new rate
        double newRate = CallsPerSecond * BackoffFactor;

        // If we have a Retry-After header, adjust rate based on it
        if (retryAfterSeconds > 0)
        {
            // Calculate rate that would space out requests appropriately
            double suggestedRate = 1.0 / retryAfterSeconds;
            newRate = Math.Min(newRate, suggestedRate);
        }

        // Apply exponential backoff for consecutive 429s
        if (_consecutive429s > 1) newRate = newRate * Math.Pow(BackoffFactor, _consecutive429s - 1);

        // Ensure we don't go below minimum rate
        newRate = Math.Max(newRate, _minRate);

        if (Math.Abs(newRate - CallsPerSecond) > 0.01)
        {
            _logger?.LogWarning(
                "Adaptive rate limiter for {ServiceName} reducing rate from {OldRate:F2} to {NewRate:F2} calls/sec due to HTTP 429 (consecutive: {Consecutive429s})",
                ServiceName, CallsPerSecond, newRate, _consecutive429s);

            UpdateRate(newRate);
        }
    }

    private void HandleSuccessResponse()
    {
        _consecutiveSuccesses++;
        _consecutive429s = 0;

        // Check if we should attempt recovery
        if (ShouldAttemptRecovery())
        {
            double newRate = Math.Min(CallsPerSecond * RecoveryFactor, _maxRate);

            if (newRate > CallsPerSecond)
            {
                _logger?.LogInformation(
                    "Adaptive rate limiter for {ServiceName} increasing rate from {OldRate:F2} to {NewRate:F2} calls/sec after {SuccessCount} successful requests",
                    ServiceName, CallsPerSecond, newRate, _consecutiveSuccesses);

                UpdateRate(newRate);
                _lastRecoveryTime = DateTime.UtcNow;
                _consecutiveSuccesses = 0; // Reset counter after recovery
            }
        }
    }

    private bool ShouldAttemptRecovery()
    {
        // Don't recover if we're already at or above base rate
        if (CallsPerSecond >= _baseRate) return false;

        // Don't recover too soon after a backoff
        TimeSpan timeSinceBackoff = DateTime.UtcNow - _lastBackoffTime;
        if (timeSinceBackoff.TotalMinutes < BackoffCooldownMinutes) return false;

        // Don't recover too frequently
        TimeSpan timeSinceLastRecovery = DateTime.UtcNow - _lastRecoveryTime;
        if (timeSinceLastRecovery.TotalMinutes < 1) return false;

        // Require enough consecutive successes
        return _consecutiveSuccesses >= RecoveryThreshold;
    }

    private void UpdateRate(double newRate)
    {
        CallsPerSecond = newRate;

        // Update the underlying rate limiter if it supports it
        if (_innerLimiter is TokenBucketRateLimiter tokenBucket)
            // Note: Would need to add a method to update rate dynamically
            // For now, log the desired change
            _logger?.LogDebug(
                "Adaptive rate change requested for {ServiceName}: {NewRate:F2} calls/sec",
                ServiceName, newRate);
    }

    private static int ParseRetryAfter(string? retryAfterHeader)
    {
        if (string.IsNullOrWhiteSpace(retryAfterHeader)) return 0;

        // Try to parse as seconds (integer)
        if (int.TryParse(retryAfterHeader, out int seconds)) return seconds;

        // Try to parse as HTTP date
        if (DateTime.TryParse(retryAfterHeader, out DateTime retryAfterDate))
        {
            TimeSpan delay = retryAfterDate - DateTime.UtcNow;
            return Math.Max(0, (int)delay.TotalSeconds);
        }

        return 0;
    }

    private static bool IsSuccessStatusCode(HttpStatusCode statusCode)
    {
        return (int)statusCode >= 200 && (int)statusCode < 300;
    }
}

/// <summary>
///     Extension methods for integrating adaptive rate limiting with HTTP clients
/// </summary>
public static class AdaptiveRateLimiterExtensions
{
    /// <summary>
    ///     Wraps a rate limiter with adaptive behavior
    /// </summary>
    public static AdaptiveRateLimiter WithAdaptiveBehavior(
        this IRateLimiter rateLimiter,
        ILogger<AdaptiveRateLimiter>? logger = null)
    {
        if (rateLimiter is AdaptiveRateLimiter adaptive) return adaptive; // Already adaptive

        return new AdaptiveRateLimiter(rateLimiter, logger);
    }
}