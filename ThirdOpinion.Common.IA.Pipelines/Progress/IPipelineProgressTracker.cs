using ThirdOpinion.Common.IA.Pipelines.Models;
using ThirdOpinion.Common.IA.Pipelines.Progress.Models;

namespace ThirdOpinion.Common.IA.Pipelines.Progress;

/// <summary>
/// In-memory progress tracking for pipeline execution
/// </summary>
public interface IPipelineProgressTracker
{
    /// <summary>
    /// Initialize the tracker for a new run
    /// </summary>
    void Initialize(Guid runId, CancellationToken ct);

    /// <summary>
    /// Record the start of a resource
    /// </summary>
    void RecordResourceStart(string resourceId, string resourceType);

    /// <summary>
    /// Record the start of a step for a resource (supports nested resources via path)
    /// </summary>
    void RecordStepStart(string[] resourcePath, string stepName);

    /// <summary>
    /// Record successful completion of a step
    /// </summary>
    void RecordStepComplete(string[] resourcePath, string stepName, int durationMs);

    /// <summary>
    /// Record step failure
    /// </summary>
    void RecordStepFailed(string[] resourcePath, string stepName, int durationMs, string? errorMessage = null);

    /// <summary>
    /// Record resource completion
    /// </summary>
    void RecordResourceComplete(
        string resourceId, 
        PipelineResourceStatus finalStatus, 
        string? errorMessage = null, 
        string? errorStep = null);

    /// <summary>
    /// Finalize tracking and persist to storage
    /// </summary>
    Task FinalizeAsync();

    /// <summary>
    /// Get current pipeline statistics
    /// </summary>
    PipelineSnapshot GetPipelineSnapshot();

    /// <summary>
    /// Log progress summary to console/logger
    /// </summary>
    void LogProgressSummary();
}

