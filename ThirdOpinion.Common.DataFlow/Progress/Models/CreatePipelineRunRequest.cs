using ThirdOpinion.Common.DataFlow.Models;

namespace ThirdOpinion.Common.DataFlow.Progress.Models;

/// <summary>
/// Request to create a new pipeline run
/// </summary>
public class CreatePipelineRunRequest
{
    /// <summary>
    /// Broad category of pipeline (e.g., "LabResult", "ClinicalFactExtraction")
    /// </summary>
    public string Category { get; set; } = string.Empty;
    
    /// <summary>
    /// Specific name/identifier for this run (e.g., "TestosteroneLabAnalysis", "PatientEligibilityCheck").
    /// Provides granular context within the category.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Type of run (Fresh, Retry, Continuation)
    /// </summary>
    public PipelineRunType RunType { get; set; }
    
    /// <summary>
    /// ID of parent run if this is a retry or continuation
    /// </summary>
    public Guid? ParentRunId { get; set; }
    
    /// <summary>
    /// Configuration for the pipeline run
    /// </summary>
    public PipelineRunConfiguration Config { get; set; } = new();
    
    /// <summary>
    /// Optional explicit run identifier; if omitted, implementations generate a new Guid.
    /// </summary>
    public Guid? RunId { get; set; }
}

