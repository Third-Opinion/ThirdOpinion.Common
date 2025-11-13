using ThirdOpinion.Common.DataFlow.Models;

namespace ThirdOpinion.Common.DataFlow.Progress.Models;

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

