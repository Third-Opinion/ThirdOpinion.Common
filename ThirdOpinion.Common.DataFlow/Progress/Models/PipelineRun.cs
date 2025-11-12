using ThirdOpinion.Common.DataFlow.Models;

namespace ThirdOpinion.Common.DataFlow.Progress.Models;

/// <summary>
/// Represents metadata for an overall pipeline run
/// </summary>
public class PipelineRun
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// Broad category of pipeline (e.g., "LabResult", "BioMarkerProgression")
    /// </summary>
    public string Category { get; set; } = string.Empty;
    
    /// <summary>
    /// Specific name/identifier for this run (e.g., "TestosteroneLabAnalysis", "PatientEligibilityCheck")
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    public PipelineRunType RunType { get; set; }
    public PipelineRunStatus Status { get; set; }
    public Guid? ParentRunId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int? DurationMs { get; set; }
}

