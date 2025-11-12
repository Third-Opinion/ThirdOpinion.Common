using ThirdOpinion.Common.DataFlow.Models;

namespace ThirdOpinion.Common.DataFlow.EntityFramework.Entities;

/// <summary>
/// Entity representing the state of a pipeline step execution for a specific resource.
/// </summary>
public class StepProgressEntity
{
    public Guid StepProgressId { get; set; }
    public Guid ResourceRunId { get; set; }
    public ResourceRunEntity ResourceRun { get; set; } = null!;
    public string StepName { get; set; } = string.Empty;
    public PipelineStepStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int? DurationMs { get; set; }
    public string? ErrorMessage { get; set; }
    public int Sequence { get; set; }
}


