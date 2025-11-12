using Microsoft.Extensions.Logging;
using ThirdOpinion.Common.DataFlow.Artifacts;
using ThirdOpinion.Common.DataFlow.Progress;

namespace ThirdOpinion.Common.DataFlow.Core;

/// <summary>
/// Context for pipeline execution, containing run metadata and optional services
/// </summary>
public interface IPipelineContext
{
    /// <summary>
    /// Unique identifier for this pipeline run
    /// </summary>
    Guid RunId { get; }

    /// <summary>
    /// Type of resource being processed
    /// </summary>
    Type ResourceType { get; }
    
    /// <summary>
    /// String representation of the resource type (typically the type name)
    /// </summary>
    string ResourceTypeName => ResourceType.Name;

    /// <summary>
    /// Broad category of pipeline (e.g., "LabResults", "ClinicalFactExtraction")
    /// </summary>
    string Category { get; }

    /// <summary>
    /// Specific name/identifier for this run (e.g., "TestosteroneLabAnalysis")
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Cancellation token for the pipeline
    /// </summary>
    CancellationToken CancellationToken { get; }

    /// <summary>
    /// Optional progress tracker for in-memory tracking
    /// </summary>
    IPipelineProgressTracker? ProgressTracker { get; }

    /// <summary>
    /// Optional artifact batcher for queuing artifact saves
    /// </summary>
    IArtifactBatcher? ArtifactBatcher { get; }

    /// <summary>
    /// Optional resource run cache for avoiding duplicate lookups
    /// </summary>
    IResourceRunCache? ResourceRunCache { get; }

    /// <summary>
    /// Logger for pipeline operations
    /// </summary>
    ILogger Logger { get; }

    /// <summary>
    /// Default step options to apply when a step doesn't specify its own
    /// </summary>
    PipelineStepOptions DefaultStepOptions { get; }
}

