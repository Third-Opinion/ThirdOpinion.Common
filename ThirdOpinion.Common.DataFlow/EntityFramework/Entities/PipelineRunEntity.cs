using ThirdOpinion.Common.DataFlow.Models;

namespace ThirdOpinion.Common.DataFlow.EntityFramework.Entities;

/// <summary>
/// EF Core entity that persists the execution metadata for a pipeline run.
/// </summary>
public class PipelineRunEntity
{
    public Guid RunId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public PipelineRunType RunType { get; set; }
    public PipelineRunStatus Status { get; set; }
    public string? Configuration { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int? DurationMs { get; set; }
    public int TotalResources { get; set; }
    public int CompletedResources { get; set; }
    public int FailedResources { get; set; }
    public int SkippedResources { get; set; }
    public Guid? ParentRunId { get; set; }
    public PipelineRunEntity? ParentRun { get; set; }
    public ICollection<PipelineRunEntity> ChildRuns { get; set; } = new List<PipelineRunEntity>();
    public ICollection<ResourceRunEntity> ResourceRuns { get; set; } = new List<ResourceRunEntity>();
}


