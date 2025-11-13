using System.Collections.Concurrent;
using ThirdOpinion.Common.IA.Pipelines.Progress;

namespace ThirdOpinion.Common.IA.Pipelines.Services.InMemory;

/// <summary>
/// In-memory implementation of IResourceRunCache for testing
/// Uses ConcurrentDictionary for thread-safe caching
/// </summary>
public class InMemoryResourceRunCache : IResourceRunCache
{
    private readonly ConcurrentDictionary<string, Guid> _cache = new();

    public Task<Guid> GetOrCreateAsync(Guid runId, string resourceId, string resourceType, CancellationToken ct)
    {
        var key = $"{runId:N}:{resourceId}";
        
        var resourceRunId = _cache.GetOrAdd(key, _ => Guid.NewGuid());
        
        return Task.FromResult(resourceRunId);
    }

    /// <summary>
    /// Get cache count (for testing/verification)
    /// </summary>
    public int GetCacheCount() => _cache.Count;

    /// <summary>
    /// Clear cache (for testing)
    /// </summary>
    public void Clear() => _cache.Clear();

    /// <summary>
    /// Check if a resource run ID exists (for testing)
    /// </summary>
    public bool Contains(Guid runId, string resourceId)
    {
        var key = $"{runId:N}:{resourceId}";
        return _cache.ContainsKey(key);
    }
}

