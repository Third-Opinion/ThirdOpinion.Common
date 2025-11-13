namespace ThirdOpinion.Common.DataFlow.Models;

/// <summary>
/// Configuration for a pipeline run
/// </summary>
public class PipelineRunConfiguration
{
    public PipelineRunType RunType { get; set; } = PipelineRunType.Fresh;
    public Guid? ParentRunId { get; set; }
    public int? StorageBatchSize { get; set; } = 100;
    public int? StorageWorkerCount { get; set; } = 3;
}

