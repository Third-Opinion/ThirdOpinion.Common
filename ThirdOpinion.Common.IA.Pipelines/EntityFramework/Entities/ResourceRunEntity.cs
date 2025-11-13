using ThirdOpinion.Common.IA.Pipelines.Models;

namespace ThirdOpinion.Common.IA.Pipelines.EntityFramework.Entities;

/// <summary>
/// Represents the execution state for a single resource being processed by a pipeline run.
/// </summary>
public class ResourceRunEntity
{
    public Guid ResourceRunId { get; set; }
    public Guid PipelineRunId { get; set; }
    public PipelineRunEntity PipelineRun { get; set; } = null!;
    public string ResourceId { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public PipelineResourceStatus Status { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int? ProcessingTimeMs { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorStep { get; set; }
    public int RetryCount { get; set; }
    public ICollection<StepProgressEntity> StepProgresses { get; set; } = new List<StepProgressEntity>();
    public ICollection<ArtifactEntity> Artifacts { get; set; } = new List<ArtifactEntity>();
}


