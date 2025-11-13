namespace ThirdOpinion.Common.IA.Pipelines.Models;

/// <summary>
/// Update for a single step within a resource's pipeline execution
/// </summary>
public class StepProgressUpdate
{
    public Guid ResourceRunId { get; set; }
    public string StepName { get; set; } = string.Empty;
    public PipelineStepStatus Status { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int? DurationMs { get; set; }
    public string? ErrorMessage { get; set; }
}

