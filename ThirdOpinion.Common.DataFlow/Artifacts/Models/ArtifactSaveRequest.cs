using ThirdOpinion.Common.DataFlow.Models;

namespace ThirdOpinion.DataFlow.Artifacts.Models;

/// <summary>
/// Request to save an artifact
/// </summary>
public class ArtifactSaveRequest
{
    /// <summary>
    /// Resource run ID
    /// </summary>
    public Guid ResourceRunId { get; set; }

    /// <summary>
    /// Step name that produced the artifact
    /// </summary>
    public string StepName { get; set; } = string.Empty;

    /// <summary>
    /// Artifact name
    /// </summary>
    public string ArtifactName { get; set; } = string.Empty;

    /// <summary>
    /// Artifact data to save
    /// </summary>
    public object Data { get; set; } = null!;

    /// <summary>
    /// Optional storage type override
    /// </summary>
    public ArtifactStorageType? StorageTypeOverride { get; set; }

    /// <summary>
    /// Internal completion token ID for tracking when artifact is persisted
    /// Used by batcher to signal completion
    /// </summary>
    public Guid CompletionTokenId { get; set; }
}

