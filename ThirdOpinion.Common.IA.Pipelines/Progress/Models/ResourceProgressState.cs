using ThirdOpinion.Common.IA.Pipelines.Models;

namespace ThirdOpinion.Common.IA.Pipelines.Progress.Models;

/// <summary>
/// Represents the progress state of a single resource through the pipeline
/// </summary>
public class ResourceProgressState
{
    public string ResourceId { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public PipelineResourceStatus Status { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public ICollection<StepProgressMetrics> StepProgresses { get; set; } = new List<StepProgressMetrics>();
    public string? ErrorMessage { get; set; }
    public string? ErrorStep { get; set; }
}

