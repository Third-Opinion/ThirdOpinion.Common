using ThirdOpinion.Common.DataFlow.Artifacts.Models;

namespace ThirdOpinion.Common.DataFlow.Artifacts;

/// <summary>
/// Interface for batching artifact save operations
/// </summary>
public interface IArtifactBatcher
{
    /// <summary>
    /// Initialize the batcher with run context
    /// </summary>
    void Initialize(Guid runId, CancellationToken ct);

    /// <summary>
    /// Queue an artifact for saving
    /// </summary>
    Task QueueArtifactSaveAsync(ArtifactSaveRequest request, CancellationToken ct = default);

    /// <summary>
    /// Finalize and flush any remaining artifacts
    /// </summary>
    Task FinalizeAsync();
}

