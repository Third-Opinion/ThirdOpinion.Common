using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using ThirdOpinion.Common.IA.Pipelines.Models;
using ThirdOpinion.Common.IA.Pipelines.Progress;
using ThirdOpinion.Common.IA.Pipelines.Progress.Models;

namespace ThirdOpinion.Common.IA.Pipelines.Services.Progress;

/// <summary>
/// Storage-agnostic resource run cache that coordinates with <see cref="IPipelineProgressService"/>.
/// </summary>
public class PipelineResourceRunCache : IResourceRunCache
{
    private readonly ConcurrentDictionary<string, Guid> _cache = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Task<Guid>> _pendingLookups = new(StringComparer.Ordinal);
    private readonly IPipelineProgressService _progressService;
    private readonly ILogger<PipelineResourceRunCache> _logger;

    public PipelineResourceRunCache(
        IPipelineProgressService progressService,
        ILogger<PipelineResourceRunCache> logger)
    {
        _progressService = progressService ?? throw new ArgumentNullException(nameof(progressService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Guid> GetOrCreateAsync(Guid runId, string resourceId, string resourceType, CancellationToken ct)
    {
        var cacheKey = CreateCacheKey(runId, resourceId);
        if (_cache.TryGetValue(cacheKey, out var existing))
        {
            return existing;
        }

        var lookupTask = _pendingLookups.GetOrAdd(cacheKey, _ => ResolveAsync(runId, resourceId, resourceType, ct));

        try
        {
            return await lookupTask.ConfigureAwait(false);
        }
        finally
        {
            _pendingLookups.TryRemove(cacheKey, out _);
        }
    }

    private async Task<Guid> ResolveAsync(Guid runId, string resourceId, string resourceType, CancellationToken ct)
    {
        var cacheKey = CreateCacheKey(runId, resourceId);

        var existingId = await _progressService
            .GetResourceRunIdAsync(runId, resourceId, ct)
            .ConfigureAwait(false);

        if (existingId.HasValue)
        {
            _cache[cacheKey] = existingId.Value;
            return existingId.Value;
        }

        var newResourceRunId = Guid.NewGuid();
        var createRequest = new ResourceProgressUpdate
        {
            ResourceRunId = newResourceRunId,
            ResourceId = resourceId,
            ResourceType = resourceType,
            Status = PipelineResourceStatus.Processing,
            StartTime = DateTime.UtcNow
        };

        await _progressService
            .CreateResourceRunsBatchAsync(runId, new[] { createRequest }, ct)
            .ConfigureAwait(false);

        var persistedId = await _progressService
            .GetResourceRunIdAsync(runId, resourceId, ct)
            .ConfigureAwait(false);

        if (!persistedId.HasValue)
        {
            _logger.LogWarning("Resource run creation did not persist for RunId={RunId}, ResourceId={ResourceId}", runId, resourceId);
            throw new InvalidOperationException("Failed to create pipeline resource run.");
        }

        _cache[cacheKey] = persistedId.Value;
        return persistedId.Value;
    }

    private static string CreateCacheKey(Guid runId, string resourceId) => $"{runId:N}:{resourceId}";
}

