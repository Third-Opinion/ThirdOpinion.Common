using ThirdOpinion.DataFlow.Artifacts.Models;

namespace ThirdOpinion.Common.DataFlow.Artifacts;

/// <summary>
/// Interface for storing artifacts
/// </summary>
public interface IArtifactStorageService
{
    /// <summary>
    /// Save a batch of artifacts
    /// </summary>
    Task<List<ArtifactSaveResult>> SaveBatchAsync(
        List<ArtifactSaveRequest> requests,
        CancellationToken ct);
}

