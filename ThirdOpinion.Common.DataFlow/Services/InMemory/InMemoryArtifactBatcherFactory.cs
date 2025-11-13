using ThirdOpinion.Common.DataFlow.Artifacts;
using ThirdOpinion.Common.DataFlow.Progress.Models;

namespace ThirdOpinion.Common.DataFlow.Services.InMemory;

/// <summary>
/// Factory for creating in-memory artifact batchers per pipeline run.
/// </summary>
public class InMemoryArtifactBatcherFactory : IArtifactBatcherFactory
{
    private readonly IArtifactStorageService _storageService;
    private readonly int _batchSize;
    private readonly int _flushIntervalMs;

    public InMemoryArtifactBatcherFactory(
        IArtifactStorageService storageService,
        int batchSize = 100,
        int flushIntervalMs = 1000)
    {
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        _batchSize = batchSize;
        _flushIntervalMs = flushIntervalMs;
    }

    public IArtifactBatcher Create(PipelineRunMetadata metadata, CancellationToken cancellationToken)
    {
        var batcher = new InMemoryArtifactBatcher(_storageService, _batchSize, _flushIntervalMs);
        batcher.Initialize(metadata.GetOrCreateRunId(), cancellationToken);
        return batcher;
    }
}

