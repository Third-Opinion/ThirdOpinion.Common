namespace ThirdOpinion.Common.DataFlow.Progress;

/// <summary>
/// Cache for resource run IDs to avoid repeated database lookups
/// </summary>
public interface IResourceRunCache
{
    /// <summary>
    /// Get or create a resource run ID
    /// </summary>
    Task<Guid> GetOrCreateAsync(Guid runId, string resourceId, string resourceType, CancellationToken ct);
}

