using ThirdOpinion.Common.IA.Pipelines.Models;

namespace ThirdOpinion.Common.IA.Pipelines.Progress.Models;

/// <summary>
/// Represents the progress and metrics for an individual pipeline step
/// </summary>
public class StepProgressMetrics
{
    public string StepName { get; set; } = string.Empty;
    public PipelineStepStatus Status { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public long? DurationMs { get; set; }
    public string? ErrorMessage { get; set; }
    public int Sequence { get; set; }
}






