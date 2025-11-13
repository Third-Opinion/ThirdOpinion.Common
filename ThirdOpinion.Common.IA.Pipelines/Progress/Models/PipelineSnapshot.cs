namespace ThirdOpinion.Common.IA.Pipelines.Progress.Models;

/// <summary>
/// Point-in-time snapshot of pipeline execution statistics
/// </summary>
public class PipelineSnapshot
{
    public Guid RunId { get; set; }
    public DateTime SnapshotTime { get; set; }
    public int TotalResources { get; set; }
    public int CompletedResources { get; set; }
    public int FailedResources { get; set; }
    public int ProcessingResources { get; set; }
}

