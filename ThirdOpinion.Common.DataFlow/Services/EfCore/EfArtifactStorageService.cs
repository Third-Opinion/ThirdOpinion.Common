using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ThirdOpinion.Common.DataFlow.Artifacts;
using ThirdOpinion.Common.DataFlow.EntityFramework.Entities;
using ThirdOpinion.Common.DataFlow.Models;
using ThirdOpinion.DataFlow.Artifacts.Models;

namespace ThirdOpinion.Common.DataFlow.Services.EfCore;

/// <summary>
/// Stores pipeline artifacts inside the DataFlow Entity Framework persistence store.
/// </summary>
public class EfArtifactStorageService : IArtifactStorageService
{
    private readonly PipelineContextPool _contextPool;
    private readonly ILogger<EfArtifactStorageService> _logger;

    public EfArtifactStorageService(
        PipelineContextPool contextPool,
        ILogger<EfArtifactStorageService> logger)
    {
        _contextPool = contextPool ?? throw new ArgumentNullException(nameof(contextPool));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<ArtifactSaveResult>> SaveBatchAsync(List<ArtifactSaveRequest> requests, CancellationToken ct)
    {
        if (requests.Count == 0)
        {
            return new List<ArtifactSaveResult>();
        }

        var dbContext = await _contextPool.RentAsync(ct).ConfigureAwait(false);
        try
        {
            var results = new List<ArtifactSaveResult>(requests.Count);
            var entities = new List<ArtifactEntity>(requests.Count);

            foreach (var request in requests)
            {
                var storageType = request.StorageTypeOverride ?? ArtifactStorageType.Database;

                entities.Add(new ArtifactEntity
                {
                    ArtifactId = Guid.NewGuid(),
                    ResourceRunId = request.ResourceRunId,
                    StepName = request.StepName,
                    ArtifactName = request.ArtifactName,
                    StorageType = storageType,
                    DataJson = SerializeArtifactData(request.Data),
                    CreatedAt = DateTime.UtcNow
                });

                results.Add(new ArtifactSaveResult
                {
                    Success = true,
                    StoragePath = $"db://{request.ResourceRunId}/{request.StepName}/{request.ArtifactName}",
                    Metadata = new Dictionary<string, object?>
                    {
                        ["storageType"] = storageType.ToString(),
                        ["resourceRunId"] = request.ResourceRunId,
                        ["stepName"] = request.StepName,
                        ["artifactName"] = request.ArtifactName
                    }
                });
            }

            await dbContext.Artifacts.AddRangeAsync(entities, ct).ConfigureAwait(false);
            await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

            return results;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Failed to persist artifact batch of size {Count}", requests.Count);
            return requests.Select(request => new ArtifactSaveResult
            {
                Success = false,
                ErrorMessage = "Failed to persist artifact batch.",
                Metadata = new Dictionary<string, object?>
                {
                    ["storageType"] = ArtifactStorageType.Database.ToString(),
                    ["resourceRunId"] = request.ResourceRunId,
                    ["stepName"] = request.StepName,
                    ["artifactName"] = request.ArtifactName
                }
            }).ToList();
        }
        finally
        {
            _contextPool.Return(dbContext);
        }
    }

    private static string? SerializeArtifactData(object data)
    {
        if (data is null)
        {
            return null;
        }

        return JsonSerializer.Serialize(data, data.GetType());
    }
}


