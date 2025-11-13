using ThirdOpinion.Common.IA.Pipelines.Models;

namespace ThirdOpinion.Common.IA.Pipelines.Progress.Models;

/// <summary>
/// Metrics for an individual pipeline step
/// </summary>
public class StepMetrics
{
    public PipelineStepStatus Status { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public long? DurationMs { get; set; }
    public string? ErrorMessage { get; set; }
}

