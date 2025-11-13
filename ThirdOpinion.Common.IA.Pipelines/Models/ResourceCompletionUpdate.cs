namespace ThirdOpinion.Common.IA.Pipelines.Models;

/// <summary>
/// Update for resource completion
/// </summary>
public class ResourceCompletionUpdate
{
    public Guid ResourceRunId { get; set; }
    public PipelineResourceStatus FinalStatus { get; set; }
    public DateTime EndTime { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorStep { get; set; }
}

