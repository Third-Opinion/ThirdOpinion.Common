using System.Collections.Concurrent;

namespace ThirdOpinion.Common.Misc.RateLimiting;

/// <summary>
/// Metrics tracking for rate limiting operations
/// </summary>
public class RateLimitMetrics
{
    private readonly ConcurrentDictionary<string, ServiceMetrics> _serviceMetrics = new();
    
    public ServiceMetrics GetMetrics(string serviceName)
    {
        return _serviceMetrics.GetOrAdd(serviceName, _ => new ServiceMetrics(serviceName));
    }
    
    public IEnumerable<ServiceMetrics> GetAllMetrics()
    {
        return _serviceMetrics.Values;
    }
    
    public void Reset(string serviceName)
    {
        if (_serviceMetrics.TryGetValue(serviceName, out var metrics))
        {
            metrics.Reset();
        }
    }
    
    public void ResetAll()
    {
        foreach (var metrics in _serviceMetrics.Values)
        {
            metrics.Reset();
        }
    }
}

/// <summary>
/// Metrics for a specific service
/// </summary>
public class ServiceMetrics
{
    private long _totalRequests;
    private long _acceptedRequests;
    private long _rejectedRequests;
    private long _throttledRequests;
    private long _totalWaitTimeMs;
    private long _maxWaitTimeMs;
    private DateTime _startTime;
    private DateTime? _lastRequestTime;
    private readonly object _lock = new();
    
    public string ServiceName { get; }
    
    public ServiceMetrics(string serviceName)
    {
        ServiceName = serviceName;
        _startTime = DateTime.UtcNow;
    }
    
    public void RecordRequest(bool accepted, long waitTimeMs = 0)
    {
        lock (_lock)
        {
            _totalRequests++;
            _lastRequestTime = DateTime.UtcNow;
            
            if (accepted)
            {
                _acceptedRequests++;
                if (waitTimeMs > 0)
                {
                    _throttledRequests++;
                    _totalWaitTimeMs += waitTimeMs;
                    _maxWaitTimeMs = Math.Max(_maxWaitTimeMs, waitTimeMs);
                }
            }
            else
            {
                _rejectedRequests++;
            }
        }
    }
    
    public RateLimitMetricsSnapshot GetSnapshot()
    {
        lock (_lock)
        {
            var uptime = DateTime.UtcNow - _startTime;
            var requestRate = uptime.TotalSeconds > 0 ? _totalRequests / uptime.TotalSeconds : 0;
            var acceptanceRate = _totalRequests > 0 ? (double)_acceptedRequests / _totalRequests * 100 : 0;
            var averageWaitTime = _throttledRequests > 0 ? _totalWaitTimeMs / (double)_throttledRequests : 0;
            
            return new RateLimitMetricsSnapshot
            {
                ServiceName = ServiceName,
                TotalRequests = _totalRequests,
                AcceptedRequests = _acceptedRequests,
                RejectedRequests = _rejectedRequests,
                ThrottledRequests = _throttledRequests,
                RequestRate = requestRate,
                AcceptanceRate = acceptanceRate,
                AverageWaitTimeMs = averageWaitTime,
                MaxWaitTimeMs = _maxWaitTimeMs,
                StartTime = _startTime,
                LastRequestTime = _lastRequestTime,
                Uptime = uptime
            };
        }
    }
    
    public void Reset()
    {
        lock (_lock)
        {
            _totalRequests = 0;
            _acceptedRequests = 0;
            _rejectedRequests = 0;
            _throttledRequests = 0;
            _totalWaitTimeMs = 0;
            _maxWaitTimeMs = 0;
            _startTime = DateTime.UtcNow;
            _lastRequestTime = null;
        }
    }
}

/// <summary>
/// Snapshot of rate limit metrics at a point in time
/// </summary>
public class RateLimitMetricsSnapshot
{
    public string ServiceName { get; init; } = string.Empty;
    public long TotalRequests { get; init; }
    public long AcceptedRequests { get; init; }
    public long RejectedRequests { get; init; }
    public long ThrottledRequests { get; init; }
    public double RequestRate { get; init; }
    public double AcceptanceRate { get; init; }
    public double AverageWaitTimeMs { get; init; }
    public long MaxWaitTimeMs { get; init; }
    public DateTime StartTime { get; init; }
    public DateTime? LastRequestTime { get; init; }
    public TimeSpan Uptime { get; init; }
    
    public override string ToString()
    {
        return $"{ServiceName}: {TotalRequests} requests ({AcceptanceRate:F1}% accepted), " +
               $"Rate: {RequestRate:F2}/sec, Avg wait: {AverageWaitTimeMs:F0}ms";
    }
}