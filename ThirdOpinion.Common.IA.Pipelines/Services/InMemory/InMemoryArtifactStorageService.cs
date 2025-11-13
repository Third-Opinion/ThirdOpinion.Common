using System.Collections.Concurrent;
using System.Text.Json;
using ThirdOpinion.Common.IA.Pipelines.Artifacts;
using ThirdOpinion.Common.IA.Pipelines.Artifacts.Models;
using ThirdOpinion.Common.IA.Pipelines.Models;

namespace ThirdOpinion.Common.IA.Pipelines.Services.InMemory;

/// <summary>
/// In-memory implementation of IArtifactStorageService for testing
/// Stores artifacts in memory as serialized JSON
/// </summary>
public class InMemoryArtifactStorageService : IArtifactStorageService
{
    private readonly ConcurrentDictionary<string, string> _artifacts = new();

    public Task<List<ArtifactSaveResult>> SaveBatchAsync(
        List<ArtifactSaveRequest> requests,
        CancellationToken ct)
    {
        var results = new List<ArtifactSaveResult>();

        foreach (var request in requests)
        {
            try
            {
                // Generate storage key
                var key = $"{request.ResourceRunId:N}/{request.StepName}/{request.ArtifactName}";

                // Serialize data to JSON
                var json = JsonSerializer.Serialize(request.Data, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                // Store in memory
                _artifacts[key] = json;

                results.Add(new ArtifactSaveResult
                {
                    Success = true,
                    StoragePath = $"memory://{key}",
                    Metadata = new Dictionary<string, object?>
                    {
                        ["storageType"] = ArtifactStorageType.Memory.ToString(),
                        ["key"] = key,
                        ["contentLength"] = json.Length
                    }
                });
            }
            catch (Exception ex)
            {
                results.Add(new ArtifactSaveResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    Metadata = new Dictionary<string, object?>
                    {
                        ["storageType"] = ArtifactStorageType.Memory.ToString(),
                        ["error"] = ex.GetType().Name
                    }
                });
            }
        }

        return Task.FromResult(results);
    }

    /// <summary>
    /// Get artifact count (for testing/verification)
    /// </summary>
    public int GetArtifactCount() => _artifacts.Count;

    /// <summary>
    /// Get artifact by key (for testing/verification)
    /// </summary>
    public string? GetArtifact(string key) => _artifacts.TryGetValue(key, out var value) ? value : null;

    /// <summary>
    /// Get all artifact keys (for testing/verification)
    /// </summary>
    public IReadOnlyCollection<string> GetAllKeys() => _artifacts.Keys.ToList();

    /// <summary>
    /// Clear all artifacts (for testing)
    /// </summary>
    public void Clear() => _artifacts.Clear();
}

