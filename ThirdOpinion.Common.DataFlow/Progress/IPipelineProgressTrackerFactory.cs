using ThirdOpinion.Common.DataFlow.Progress.Models;

namespace ThirdOpinion.Common.DataFlow.Progress;

/// <summary>
/// Factory for creating IPipelineProgressTracker instances with run metadata
/// </summary>
public interface IPipelineProgressTrackerFactory
{
    /// <summary>
    /// Create a new progress tracker initialized with the provided metadata
    /// </summary>
    /// <param name="metadata">Run metadata including RunId, Category, and Name</param>
    /// <param name="cancellationToken">Cancellation token for the pipeline run</param>
    /// <returns>Initialized progress tracker</returns>
    IPipelineProgressTracker Create(PipelineRunMetadata metadata, CancellationToken cancellationToken);
}

