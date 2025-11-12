using ThirdOpinion.Common.DataFlow.Progress.Models;

namespace ThirdOpinion.Common.DataFlow.Artifacts;

/// <summary>
/// Factory for creating artifact batcher instances scoped to a pipeline run.
/// </summary>
public interface IArtifactBatcherFactory
{
    /// <summary>
    /// Create and initialize an artifact batcher for the provided run metadata.
    /// </summary>
    /// <param name="metadata">Run metadata containing RunId, Category, and Name.</param>
    /// <param name="cancellationToken">Cancellation token for the pipeline run.</param>
    /// <returns>An initialized artifact batcher instance.</returns>
    IArtifactBatcher Create(PipelineRunMetadata metadata, CancellationToken cancellationToken);
}

