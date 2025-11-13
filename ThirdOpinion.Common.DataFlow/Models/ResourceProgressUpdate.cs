namespace ThirdOpinion.Common.DataFlow.Models;

/// <summary>
/// Batch update for resource progress
/// </summary>
public class ResourceProgressUpdate
{
    public Guid ResourceRunId { get; set; }
    public string ResourceId { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public PipelineResourceStatus Status { get; set; }
    public DateTime StartTime { get; set; }
}

