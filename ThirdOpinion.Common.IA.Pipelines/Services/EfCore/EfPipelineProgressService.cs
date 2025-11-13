using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ThirdOpinion.Common.IA.Pipelines.EntityFramework;
using ThirdOpinion.Common.IA.Pipelines.EntityFramework.Entities;
using ThirdOpinion.Common.IA.Pipelines.Models;
using ThirdOpinion.Common.IA.Pipelines.Progress;
using ThirdOpinion.Common.IA.Pipelines.Progress.Models;

namespace ThirdOpinion.Common.IA.Pipelines.Services.EfCore;

/// <summary>
/// Entity Framework Core backed implementation of <see cref="IPipelineProgressService"/>.
/// </summary>
public class EfPipelineProgressService : IPipelineProgressService
{
    private readonly PipelineContextPool _contextPool;
    private readonly ILogger<EfPipelineProgressService> _logger;

    public EfPipelineProgressService(
        PipelineContextPool contextPool,
        ILogger<EfPipelineProgressService> logger)
    {
        _contextPool = contextPool ?? throw new ArgumentNullException(nameof(contextPool));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PipelineRun> CreateRunAsync(CreatePipelineRunRequest request, CancellationToken ct)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        var dbContext = await _contextPool.RentAsync(ct).ConfigureAwait(false);
        try
        {
            var runEntity = new PipelineRunEntity
            {
                RunId = request.RunId ?? Guid.NewGuid(),
                Category = request.Category,
                Name = request.Name,
                RunType = request.RunType,
                Status = PipelineRunStatus.Pending,
                ParentRunId = request.ParentRunId,
                Configuration = request.Config is null ? null : System.Text.Json.JsonSerializer.Serialize(request.Config),
                StartTime = DateTime.UtcNow
            };

            dbContext.PipelineRuns.Add(runEntity);
            await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

            _logger.LogInformation("Created pipeline run {RunId} ({Category}/{Name})", runEntity.RunId, runEntity.Category, runEntity.Name);

            return new PipelineRun
            {
                Id = runEntity.RunId,
                Category = runEntity.Category,
                Name = runEntity.Name,
                RunType = runEntity.RunType,
                Status = runEntity.Status,
                ParentRunId = runEntity.ParentRunId,
                StartTime = runEntity.StartTime
            };
        }
        finally
        {
            _contextPool.Return(dbContext);
        }
    }

    public async Task CompleteRunAsync(Guid runId, PipelineRunStatus finalStatus, CancellationToken ct)
    {
        var dbContext = await _contextPool.RentAsync(ct).ConfigureAwait(false);
        try
        {
            var run = await dbContext.PipelineRuns
                .SingleOrDefaultAsync(r => r.RunId == runId, ct)
                .ConfigureAwait(false);

            if (run == null)
            {
                _logger.LogWarning("Attempted to complete unknown pipeline run {RunId}", runId);
                return;
            }

            var resourceStats = await dbContext.ResourceRuns
                .Where(r => r.PipelineRunId == runId)
                .GroupBy(r => 1)
                .Select(g => new
                {
                    Total = g.Count(),
                    Completed = g.Count(r => r.Status == PipelineResourceStatus.Completed),
                    Failed = g.Count(r => r.Status == PipelineResourceStatus.Failed),
                    Cancelled = g.Count(r => r.Status == PipelineResourceStatus.Cancelled)
                })
                .SingleOrDefaultAsync(ct)
                .ConfigureAwait(false);

            run.Status = finalStatus;
            run.EndTime = DateTime.UtcNow;
            run.DurationMs = (int?)((run.EndTime.Value - run.StartTime).TotalMilliseconds);
            run.TotalResources = resourceStats?.Total ?? 0;
            run.CompletedResources = resourceStats?.Completed ?? 0;
            run.FailedResources = resourceStats?.Failed ?? 0;
            run.SkippedResources = resourceStats?.Cancelled ?? 0;

            await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

            _logger.LogInformation("Completed pipeline run {RunId} with status {Status}", runId, finalStatus);
        }
        finally
        {
            _contextPool.Return(dbContext);
        }
    }

    public async Task<List<string>> GetIncompleteResourceIdsAsync(Guid parentRunId, CancellationToken ct)
    {
        var dbContext = await _contextPool.RentAsync(ct).ConfigureAwait(false);
        try
        {
            return await dbContext.ResourceRuns
                .Where(r => r.PipelineRunId == parentRunId &&
                            r.Status != PipelineResourceStatus.Completed &&
                            r.Status != PipelineResourceStatus.Cancelled)
                .Select(r => r.ResourceId)
                .ToListAsync(ct)
                .ConfigureAwait(false);
        }
        finally
        {
            _contextPool.Return(dbContext);
        }
    }

    public async Task<Guid?> GetResourceRunIdAsync(Guid runId, string resourceId, CancellationToken ct)
    {
        var dbContext = await _contextPool.RentAsync(ct).ConfigureAwait(false);
        try
        {
            var entity = await dbContext.ResourceRuns
                .AsNoTracking()
                .Where(r => r.PipelineRunId == runId && r.ResourceId == resourceId)
                .Select(r => r.ResourceRunId)
                .SingleOrDefaultAsync(ct)
                .ConfigureAwait(false);

            return entity == Guid.Empty ? null : entity;
        }
        finally
        {
            _contextPool.Return(dbContext);
        }
    }

    public async Task CreateResourceRunsBatchAsync(Guid runId, ResourceProgressUpdate[] updates, CancellationToken ct)
    {
        if (updates.Length == 0)
            return;

        var dbContext = await _contextPool.RentAsync(ct).ConfigureAwait(false);
        try
        {
            var pipelineRun = await dbContext.PipelineRuns
                .SingleOrDefaultAsync(r => r.RunId == runId, ct)
                .ConfigureAwait(false);

            if (pipelineRun == null)
            {
                _logger.LogWarning("Pipeline run {RunId} not found when registering {Count} resource runs. Deferring batch.", runId, updates.Length);
                return;
            }

            var requestedRunIds = updates.Select(update => update.ResourceRunId).ToList();
            var requestedResourceIds = updates.Select(update => update.ResourceId).ToList();

            var existingEntries = await dbContext.ResourceRuns
                .Where(r => r.PipelineRunId == runId &&
                            (requestedRunIds.Contains(r.ResourceRunId) || requestedResourceIds.Contains(r.ResourceId)))
                .Select(r => new { r.ResourceRunId, r.ResourceId })
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var seenRunIds = new HashSet<Guid>(existingEntries.Select(e => e.ResourceRunId));
            var seenResourceIds = new HashSet<string>(existingEntries.Select(e => e.ResourceId), StringComparer.Ordinal);
            var newEntities = new List<ResourceRunEntity>(updates.Length);

            foreach (var update in updates)
            {
                var duplicateRunId = !seenRunIds.Add(update.ResourceRunId);
                var duplicateResourceId = !seenResourceIds.Add(update.ResourceId);

                if (duplicateRunId || duplicateResourceId)
                {
                    _logger.LogDebug("Skipping duplicate resource registration for pipeline {RunId} (ResourceRunId={ResourceRunId}, ResourceId={ResourceId})", runId, update.ResourceRunId, update.ResourceId);
                    continue;
                }

                newEntities.Add(new ResourceRunEntity
                {
                    ResourceRunId = update.ResourceRunId,
                    PipelineRunId = runId,
                    ResourceId = update.ResourceId,
                    ResourceType = update.ResourceType,
                    Status = update.Status,
                    StartTime = update.StartTime
                });
            }

            if (newEntities.Count == 0)
            {
                if (updates.Length > 0)
                {
                    _logger.LogDebug("All resource runs in batch for pipeline {RunId} were already registered.", runId);
                }
                return;
            }

            await dbContext.ResourceRuns.AddRangeAsync(newEntities, ct).ConfigureAwait(false);

            var distinctResourcesAdded = newEntities
                .Select(entity => entity.ResourceId)
                .Distinct(StringComparer.Ordinal)
                .Count();

            pipelineRun.TotalResources += distinctResourcesAdded;
            if (pipelineRun.Status == PipelineRunStatus.Pending)
            {
                pipelineRun.Status = PipelineRunStatus.Running;
            }

            await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Failed to create resource runs for pipeline run {RunId}", runId);
            throw;
        }
        finally
        {
            _contextPool.Return(dbContext);
        }
    }

    public async Task<IReadOnlyList<StepProgressUpdate>> UpdateStepProgressBatchAsync(Guid runId, StepProgressUpdate[] updates, CancellationToken ct)
    {
        if (updates.Length == 0)
            return Array.Empty<StepProgressUpdate>();

        var resourceRunIds = updates.Select(u => u.ResourceRunId).Distinct().ToList();

        var dbContext = await _contextPool.RentAsync(ct).ConfigureAwait(false);
        try
        {
            var resourceRuns = await dbContext.ResourceRuns
                .Where(r => resourceRunIds.Contains(r.ResourceRunId))
                .Select(r => r.ResourceRunId)
                .ToHashSetAsync(ct)
                .ConfigureAwait(false);

            if (resourceRuns.Count == 0)
            {
                _logger.LogWarning("Resource runs not yet available for {Count} step progress updates on pipeline run {RunId}. Deferring batch.", updates.Length, runId);
                return updates;
            }

            var existingSequences = await dbContext.StepProgresses
                .Where(sp => resourceRunIds.Contains(sp.ResourceRunId))
                .GroupBy(sp => sp.ResourceRunId)
                .Select(g => new { ResourceRunId = g.Key, MaxSequence = g.Max(sp => (int?)sp.Sequence) ?? -1 })
                .ToDictionaryAsync(g => g.ResourceRunId, g => g.MaxSequence, ct)
                .ConfigureAwait(false);

            var newEntities = new List<StepProgressEntity>(updates.Length);

            var deferred = new List<StepProgressUpdate>();

            foreach (var group in updates.GroupBy(u => u.ResourceRunId))
            {
                if (!resourceRuns.Contains(group.Key))
                {
                    deferred.AddRange(group);
                    continue;
                }

                var sequence = existingSequences.TryGetValue(group.Key, out var currentSequence)
                    ? currentSequence
                    : -1;

                foreach (var update in group.OrderBy(u => u.EndTime ?? u.StartTime ?? DateTime.UtcNow))
                {
                    sequence += 1;
                    existingSequences[group.Key] = sequence;

                    var endTime = update.EndTime;
                    var startTime = update.StartTime;
                    if (startTime == null && endTime != null && update.DurationMs.HasValue)
                    {
                        startTime = endTime.Value.AddMilliseconds(-update.DurationMs.Value);
                    }

                    newEntities.Add(new StepProgressEntity
                    {
                        StepProgressId = Guid.NewGuid(),
                        ResourceRunId = update.ResourceRunId,
                        StepName = update.StepName,
                        Status = update.Status,
                        CreatedAt = DateTime.UtcNow,
                        StartTime = startTime,
                        EndTime = endTime,
                        DurationMs = update.DurationMs,
                        ErrorMessage = update.ErrorMessage,
                        Sequence = sequence
                    });
                }
            }

            await dbContext.StepProgresses.AddRangeAsync(newEntities, ct).ConfigureAwait(false);
            await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

            if (deferred.Count > 0)
            {
                _logger.LogDebug("Deferred {Count} step progress updates for pipeline run {RunId} because resource runs are not yet registered.", deferred.Count, runId);
            }

            return deferred;
        }
        finally
        {
            _contextPool.Return(dbContext);
        }
    }

    public async Task CompleteResourceRunsBatchAsync(Guid runId, ResourceCompletionUpdate[] updates, CancellationToken ct)
    {
        if (updates.Length == 0)
            return;

        var resourceRunIds = updates.Select(u => u.ResourceRunId).Distinct().ToList();

        var dbContext = await _contextPool.RentAsync(ct).ConfigureAwait(false);
        try
        {
            var resourceRuns = await dbContext.ResourceRuns
                .Where(r => resourceRunIds.Contains(r.ResourceRunId))
                .ToDictionaryAsync(r => r.ResourceRunId, ct)
                .ConfigureAwait(false);

            foreach (var update in updates)
            {
                if (!resourceRuns.TryGetValue(update.ResourceRunId, out var resourceRun))
                {
                    _logger.LogWarning("Resource run {ResourceRunId} not found when completing pipeline run {RunId}", update.ResourceRunId, runId);
                    continue;
                }

                resourceRun.Status = update.FinalStatus;
                resourceRun.EndTime = update.EndTime;
                resourceRun.ProcessingTimeMs = resourceRun.StartTime.HasValue
                    ? (int?)(update.EndTime - resourceRun.StartTime.Value).TotalMilliseconds
                    : null;
                resourceRun.ErrorMessage = update.ErrorMessage;
                resourceRun.ErrorStep = update.ErrorStep;
            }

            await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

            // Refresh aggregate counts for the run
            await CompleteRunAsyncInternal(dbContext, runId, ct).ConfigureAwait(false);
        }
        finally
        {
            _contextPool.Return(dbContext);
        }
    }

    private async Task CompleteRunAsyncInternal(IDataFlowDbContext dbContext, Guid runId, CancellationToken ct)
    {
        var run = await dbContext.PipelineRuns
            .SingleOrDefaultAsync(r => r.RunId == runId, ct)
            .ConfigureAwait(false);

        if (run == null)
        {
            return;
        }

        var stats = await dbContext.ResourceRuns
            .Where(r => r.PipelineRunId == runId)
            .GroupBy(r => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Completed = g.Count(r => r.Status == PipelineResourceStatus.Completed),
                Failed = g.Count(r => r.Status == PipelineResourceStatus.Failed),
                Cancelled = g.Count(r => r.Status == PipelineResourceStatus.Cancelled)
            })
            .SingleOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (stats != null)
        {
            run.TotalResources = stats.Total;
            run.CompletedResources = stats.Completed;
            run.FailedResources = stats.Failed;
            run.SkippedResources = stats.Cancelled;
            await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }
}


