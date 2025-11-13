using ThirdOpinion.Common.IA.Pipelines.Models;

namespace ThirdOpinion.Common.IA.Pipelines.Progress.Models;

/// <summary>
/// Metadata for a pipeline run used to initialize progress tracking
/// </summary>
public class PipelineRunMetadata
{
    /// <summary>
    /// Unique identifier for this pipeline run. If not provided, a new Guid will be generated.
    /// </summary>
    public Guid? RunId { get; set; }

    /// <summary>
    /// Broad category of pipeline (e.g., "LabResults", "ClinicalFactExtraction")
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Specific name/identifier for this run (e.g., "TestosteroneLabAnalysis", "PatientEligibilityCheck")
    /// Provides granular context within the category.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Run type indicates whether this is a fresh execution, retry, continuation, etc.
    /// </summary>
    public PipelineRunType RunType { get; set; } = PipelineRunType.Fresh;

    /// <summary>
    /// Optional parent run identifier when performing retries or continuations.
    /// </summary>
    public Guid? ParentRunId { get; set; }

    /// <summary>
    /// Get the effective RunId, generating a new one if not provided
    /// </summary>
    public Guid GetOrCreateRunId() => RunId ?? Guid.NewGuid();
}

