using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ThirdOpinion.Common.DataFlow.EntityFramework.Entities;
using ThirdOpinion.Common.DataFlow.Models;
using ThirdOpinion.Common.DataFlow.Progress;

namespace ThirdOpinion.Common.DataFlow.Services.EfCore;

/// <summary>
/// EF Core backed implementation of <see cref="IResourceRunCache"/>.
/// </summary>
public class EfResourceRunCache : IResourceRunCache
{
    private readonly ConcurrentDictionary<string, Guid> _cache = new();
    private readonly ConcurrentDictionary<string, Task<Guid>> _pendingLookups = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly PipelineContextPool _contextPool;
    private readonly ILogger<EfResourceRunCache> _logger;

    public EfResourceRunCache(
        PipelineContextPool contextPool,
        ILogger<EfResourceRunCache> logger)
    {
        _contextPool = contextPool ?? throw new ArgumentNullException(nameof(contextPool));
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

        await _semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_cache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            var dbContext = await _contextPool.RentAsync(ct).ConfigureAwait(false);
            try
            {
                var entity = await dbContext.ResourceRuns
                    .SingleOrDefaultAsync(r => r.PipelineRunId == runId && r.ResourceId == resourceId, ct)
                    .ConfigureAwait(false);

                if (entity == null)
                {
                    entity = new ResourceRunEntity
                    {
                        ResourceRunId = Guid.NewGuid(),
                        PipelineRunId = runId,
                        ResourceId = resourceId,
                        ResourceType = resourceType,
                        Status = PipelineResourceStatus.Processing,
                        StartTime = DateTime.UtcNow
                    };

                    await dbContext.ResourceRuns.AddAsync(entity, ct).ConfigureAwait(false);

                    var pipelineRun = await dbContext.PipelineRuns
                        .SingleOrDefaultAsync(r => r.RunId == runId, ct)
                        .ConfigureAwait(false);

                    if (pipelineRun != null)
                    {
                        pipelineRun.TotalResources += 1;
                        if (pipelineRun.Status == PipelineRunStatus.Pending)
                        {
                            pipelineRun.Status = PipelineRunStatus.Running;
                        }
                    }

                    await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
                }

                _cache[cacheKey] = entity.ResourceRunId;
                return entity.ResourceRunId;
            }
            finally
            {
                _contextPool.Return(dbContext);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private static string CreateCacheKey(Guid runId, string resourceId) => $"{runId:N}:{resourceId}";
}


