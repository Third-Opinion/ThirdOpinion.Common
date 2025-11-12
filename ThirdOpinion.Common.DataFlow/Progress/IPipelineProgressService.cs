using System.Collections.Generic;
using ThirdOpinion.Common.DataFlow.Models;
using ThirdOpinion.Common.DataFlow.Progress.Models;

namespace ThirdOpinion.Common.DataFlow.Progress;

/// <summary>
/// Service for persisting pipeline progress to a database or other storage
/// </summary>
public interface IPipelineProgressService
{
    /// <summary>
    /// Create a new pipeline run
    /// </summary>
    Task<PipelineRun> CreateRunAsync(CreatePipelineRunRequest request, CancellationToken ct);

    /// <summary>
    /// Mark a pipeline run as complete
    /// </summary>
    Task CompleteRunAsync(Guid runId, PipelineRunStatus finalStatus, CancellationToken ct);

    /// <summary>
    /// Get IDs of resources that haven't completed from a previous run
    /// </summary>
    Task<List<string>> GetIncompleteResourceIdsAsync(Guid parentRunId, CancellationToken ct);

    /// <summary>
    /// Create resource runs in batch
    /// </summary>
    Task CreateResourceRunsBatchAsync(Guid runId, ResourceProgressUpdate[] updates, CancellationToken ct);

    /// <summary>
    /// Update step progress in batch.
    /// Returns any updates that could not be persisted because their corresponding resource runs have
    /// not been created yet (common during highly concurrent processing). The caller should retry these.
    /// </summary>
    Task<IReadOnlyList<StepProgressUpdate>> UpdateStepProgressBatchAsync(Guid runId, StepProgressUpdate[] updates, CancellationToken ct);

    /// <summary>
    /// Complete resource runs in batch
    /// </summary>
    Task CompleteResourceRunsBatchAsync(Guid runId, ResourceCompletionUpdate[] updates, CancellationToken ct);
}

