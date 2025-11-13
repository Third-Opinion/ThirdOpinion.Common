namespace ThirdOpinion.Common.IA.Pipelines.Models;

/// <summary>
/// Status of a resource throughout the pipeline
/// </summary>
public enum PipelineResourceStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Status of an individual pipeline step
/// </summary>
public enum PipelineStepStatus
{
    Pending,
    InProgress,
    Completed,
    Failed
}

/// <summary>
/// Type of storage used for artifacts
/// </summary>
public enum ArtifactStorageType
{
    S3,
    Database,
    FileSystem,
    Memory
}

/// <summary>
/// Type of pipeline run
/// </summary>
public enum PipelineRunType
{
    Fresh,
    Retry,
    Continuation
}

/// <summary>
/// Overall status of a pipeline run
/// </summary>
public enum PipelineRunStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}

