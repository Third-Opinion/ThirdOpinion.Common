namespace ThirdOpinion.Common.DataFlow.Progress.Models;

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
    /// Get the effective RunId, generating a new one if not provided
    /// </summary>
    public Guid GetOrCreateRunId() => RunId ?? Guid.NewGuid();
}

