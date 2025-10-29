using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace ThirdOpinion.Common.Misc.RateLimiting;

/// <summary>
/// Token bucket implementation of rate limiting
/// </summary>
public class TokenBucketRateLimiter : IRateLimiter, IDisposable
{
    private readonly SemaphoreSlim _semaphore;
    private readonly object _lock = new();
    private readonly ILogger<TokenBucketRateLimiter>? _logger;
    private readonly Timer _refillTimer;
    private readonly RateLimitConfig _config;
    private readonly ServiceMetrics? _metrics;
    
    private double _tokens;
    private DateTime _lastRefillTime;
    private int _waitingRequests;
    private bool _disposed;

    public string ServiceName => _config.ServiceName;
    public double CallsPerSecond => _config.CallsPerSecond;

    public TokenBucketRateLimiter(RateLimitConfig config, ILogger<TokenBucketRateLimiter>? logger = null, ServiceMetrics? metrics = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger;
        _metrics = metrics;
        
        var burstSize = config.GetBurstSize();
        _tokens = burstSize;
        _lastRefillTime = DateTime.UtcNow;
        _semaphore = new SemaphoreSlim(burstSize, burstSize);
        
        // Calculate refill interval - use smaller intervals for smoother rate limiting
        var refillIntervalMs = Math.Min(1000, Math.Max(50, (int)(1000 / config.CallsPerSecond)));
        _refillTimer = new Timer(RefillTokens, null, refillIntervalMs, refillIntervalMs);
        
        _logger?.LogInformation(
            "TokenBucketRateLimiter initialized for {ServiceName}: {CallsPerSecond} calls/sec, burst size {BurstSize}, refill interval {RefillInterval}ms",
            config.ServiceName, config.CallsPerSecond, burstSize, refillIntervalMs);
    }

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        if (!_config.Enabled)
        {
            return; // Rate limiting disabled, allow all requests
        }

        Interlocked.Increment(ref _waitingRequests);
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            await _semaphore.WaitAsync(cancellationToken);
            
            lock (_lock)
            {
                if (_tokens >= 1)
                {
                    _tokens--;
                    var waitTimeMs = stopwatch.ElapsedMilliseconds;
                    _metrics?.RecordRequest(true, waitTimeMs);
                    
                    if (waitTimeMs > 100)
                    {
                        _logger?.LogWarning(
                            "Rate limiter for {ServiceName} delayed request by {DelayMs}ms. Available tokens: {Tokens}",
                            ServiceName, waitTimeMs, (int)_tokens);
                    }
                }
                else
                {
                    // This shouldn't happen with proper semaphore management, but handle it gracefully
                    _metrics?.RecordRequest(false);
                    _logger?.LogError(
                        "Token bucket for {ServiceName} allowed request but had no tokens available. This indicates a synchronization issue.",
                        ServiceName);
                }
            }
        }
        finally
        {
            Interlocked.Decrement(ref _waitingRequests);
        }
    }

    public bool TryAcquire()
    {
        if (!_config.Enabled)
        {
            return true;
        }

        lock (_lock)
        {
            if (_tokens >= 1 && _semaphore.CurrentCount > 0)
            {
                if (_semaphore.Wait(0))
                {
                    _tokens--;
                    _metrics?.RecordRequest(true);
                    return true;
                }
            }
        }
        
        _metrics?.RecordRequest(false);
        return false;
    }

    public async Task<bool> TryAcquireAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (!_config.Enabled)
        {
            return true;
        }

        Interlocked.Increment(ref _waitingRequests);
        
        try
        {
            var acquired = await _semaphore.WaitAsync(timeout, cancellationToken);
            
            if (acquired)
            {
                lock (_lock)
                {
                    if (_tokens >= 1)
                    {
                        _tokens--;
                        _metrics?.RecordRequest(true, (long)timeout.TotalMilliseconds);
                    }
                    else
                    {
                        _metrics?.RecordRequest(false);
                    }
                }
            }
            
            return acquired;
        }
        finally
        {
            Interlocked.Decrement(ref _waitingRequests);
        }
    }

    public RateLimitStatus GetStatus()
    {
        lock (_lock)
        {
            return new RateLimitStatus
            {
                AvailableTokens = (int)_tokens,
                MaxTokens = _config.GetBurstSize(),
                NextRefillTime = _lastRefillTime.AddMilliseconds(1000 / _config.CallsPerSecond),
                CurrentRate = _config.CallsPerSecond,
                ServiceName = ServiceName,
                WaitingRequests = _waitingRequests
            };
        }
    }

    private void RefillTokens(object? state)
    {
        if (_disposed)
        {
            return;
        }

        lock (_lock)
        {
            var now = DateTime.UtcNow;
            var elapsed = (now - _lastRefillTime).TotalSeconds;
            
            if (elapsed <= 0)
            {
                return; // Protect against clock adjustments
            }

            // Calculate tokens to add based on elapsed time
            var tokensToAdd = elapsed * _config.CallsPerSecond;
            var maxTokens = _config.GetBurstSize();
            
            var previousTokens = _tokens;
            _tokens = Math.Min(_tokens + tokensToAdd, maxTokens);
            _lastRefillTime = now;
            
            // Release semaphore permits for the new tokens
            var newTokens = (int)_tokens - (int)previousTokens;
            if (newTokens > 0)
            {
                try
                {
                    _semaphore.Release(newTokens);
                    
                    if (_waitingRequests > 0)
                    {
                        _logger?.LogDebug(
                            "Rate limiter for {ServiceName} refilled {TokenCount} tokens. Waiting requests: {WaitingRequests}",
                            ServiceName, newTokens, _waitingRequests);
                    }
                }
                catch (SemaphoreFullException)
                {
                    // This can happen if the semaphore is already at max capacity
                    // It's safe to ignore as it means we already have enough permits
                }
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _refillTimer?.Dispose();
        _semaphore?.Dispose();
    }
}