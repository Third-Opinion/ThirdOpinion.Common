using ThirdOpinion.Common.IA.Pipelines.Models;

namespace ThirdOpinion.Common.IA.Pipelines.EntityFramework.Entities;

/// <summary>
/// Represents an artifact emitted by a pipeline step for a resource run.
/// </summary>
public class ArtifactEntity
{
    public Guid ArtifactId { get; set; }
    public Guid ResourceRunId { get; set; }
    public ResourceRunEntity ResourceRun { get; set; } = null!;
    public string StepName { get; set; } = string.Empty;
    public string ArtifactName { get; set; } = string.Empty;
    public ArtifactStorageType StorageType { get; set; }
    public string? StoragePath { get; set; }
    public string? DataJson { get; set; }
    public string? MetadataJson { get; set; }
    public DateTime CreatedAt { get; set; }
}


