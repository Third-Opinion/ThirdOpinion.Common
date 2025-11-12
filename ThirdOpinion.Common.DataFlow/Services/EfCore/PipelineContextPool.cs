using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThirdOpinion.Common.DataFlow.EntityFramework;

namespace ThirdOpinion.Common.DataFlow.Services.EfCore;

/// <summary>
/// Provides a bounded pool for leasing <see cref="IDataFlowDbContext"/> instances to pipeline services.
/// </summary>
public class PipelineContextPool : IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PipelineContextPool> _logger;
    private readonly PipelineContextPoolOptions _options;
    private readonly SemaphoreSlim _semaphore;
    private readonly ConcurrentDictionary<IDataFlowDbContext, IServiceScope> _contextScopes;
    private long _totalRentals;
    private long _totalReturns;
    private long _activeContexts;
    private bool _disposed;

    public PipelineContextPool(
        IServiceScopeFactory scopeFactory,
        IOptions<PipelineContextPoolOptions> options,
        ILogger<PipelineContextPool> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

        _semaphore = new SemaphoreSlim(_options.MaxConcurrentContexts, _options.MaxConcurrentContexts);
        _contextScopes = new ConcurrentDictionary<IDataFlowDbContext, IServiceScope>(ReferenceEqualityComparer<IDataFlowDbContext>.Instance);

        _logger.LogInformation("DataFlow pipeline context pool initialized. Max concurrent contexts: {MaxConcurrentContexts}",
            _options.MaxConcurrentContexts);
    }

    /// <summary>
    /// Lease a DbContext instance from the pool.
    /// </summary>
    public async Task<IDataFlowDbContext> RentAsync(CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();

        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        if (_disposed)
        {
            _semaphore.Release();
            throw new ObjectDisposedException(nameof(PipelineContextPool));
        }

        var scope = _scopeFactory.CreateScope();

        try
        {
            var context = scope.ServiceProvider.GetRequiredService<IDataFlowDbContext>();

            if (!_contextScopes.TryAdd(context, scope))
            {
                scope.Dispose();
                _semaphore.Release();
                throw new InvalidOperationException("Failed to track leased IDataFlowDbContext for pooling.");
            }

            Interlocked.Increment(ref _activeContexts);
            Interlocked.Increment(ref _totalRentals);

            _logger.LogTrace("Context leased from pool. Active: {Active}, Available: {Available}",
                _activeContexts, _semaphore.CurrentCount);

            return context;
        }
        catch
        {
            scope.Dispose();
            _semaphore.Release();
            throw;
        }
    }

    /// <summary>
    /// Return a previously leased DbContext to the pool.
    /// </summary>
    public void Return(IDataFlowDbContext? context)
    {
        if (context == null)
        {
            _semaphore.Release();
            return;
        }

        try
        {
            if (_contextScopes.TryRemove(context, out var scope))
            {
                context.Dispose();
                scope.Dispose();
            }
            else
            {
                _logger.LogWarning("Attempted to return a context that was not rented from the pool.");
            }
        }
        finally
        {
            Interlocked.Decrement(ref _activeContexts);
            Interlocked.Increment(ref _totalReturns);
            _semaphore.Release();

            _logger.LogTrace("Context returned to pool. Active: {Active}, Available: {Available}",
                _activeContexts, _semaphore.CurrentCount);
        }
    }

    /// <summary>
    /// Gets a snapshot of the current pool statistics.
    /// </summary>
    public PoolStatistics GetStatistics()
    {
        return new PoolStatistics(
            _options.MaxConcurrentContexts,
            (int)_activeContexts,
            _semaphore.CurrentCount,
            _totalRentals,
            _totalReturns);
    }

    /// <summary>
    /// Log current pool statistics at information level.
    /// </summary>
    public void LogPoolStatistics()
    {
        var stats = GetStatistics();
        _logger.LogInformation(
            "Pipeline context pool - Max: {Max}, Active: {Active}, Available: {Available}, Total Rentals: {Rentals}, Total Returns: {Returns}",
            stats.MaxConcurrentContexts,
            stats.ActiveContexts,
            stats.AvailableContexts,
            stats.TotalRentals,
            stats.TotalReturns);
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(PipelineContextPool));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _semaphore.Dispose();

        _logger.LogInformation("Pipeline context pool disposed.");
    }
}

/// <summary>
/// Immutable statistics snapshot for the pipeline context pool.
/// </summary>
/// <param name="MaxConcurrentContexts">Maximum number of concurrent contexts allowed.</param>
/// <param name="ActiveContexts">Number of contexts currently leased.</param>
/// <param name="AvailableContexts">Number of contexts currently available for lease.</param>
/// <param name="TotalRentals">Total number of rentals since start.</param>
/// <param name="TotalReturns">Total number of returns since start.</param>
public readonly record struct PoolStatistics(
    int MaxConcurrentContexts,
    int ActiveContexts,
    int AvailableContexts,
    long TotalRentals,
    long TotalReturns);

internal sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T>
    where T : class
{
    public static ReferenceEqualityComparer<T> Instance { get; } = new();

    public bool Equals(T? x, T? y) => ReferenceEquals(x, y);

    public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
}


