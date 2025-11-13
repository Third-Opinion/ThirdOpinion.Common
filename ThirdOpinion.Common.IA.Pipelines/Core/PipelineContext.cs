using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ThirdOpinion.Common.IA.Pipelines.Progress.Models;
using System.Threading.Tasks.Dataflow;
using System.Linq;
using System.Threading.Tasks;
using ThirdOpinion.Common.IA.Pipelines.Artifacts;
using ThirdOpinion.Common.IA.Pipelines.Models;
using ThirdOpinion.Common.IA.Pipelines.Progress;

namespace ThirdOpinion.Common.IA.Pipelines.Core;

/// <summary>
/// Default implementation of IPipelineContext
/// </summary>
public class PipelineContext : IPipelineContext, IAsyncDisposable
{
    public Guid RunId { get; }
    public Type ResourceType { get; }
    public string ResourceTypeName => ResourceType.Name;
    public string Category { get; }
    public string Name { get; }
    public CancellationToken CancellationToken { get; }
    public IPipelineProgressTracker? ProgressTracker { get; }
    public IArtifactBatcher? ArtifactBatcher { get; }
    public IResourceRunCache? ResourceRunCache { get; }
    public ILogger Logger { get; }
    public PipelineRunType RunType { get; }
    public Guid? ParentRunId { get; }
    private readonly PipelineStepOptions _defaultStepOptions;
    public PipelineStepOptions DefaultStepOptions => _defaultStepOptions.Clone();
    
    private bool _disposed;
    private readonly List<IDataflowBlock> _artifactBlocks = new();
    private bool _artifactBatcherFinalized;

    internal void RegisterArtifactBlock(IDataflowBlock block)
    {
        if (block == null) return;
        _artifactBlocks.Add(block);
    }

    internal Task WaitForArtifactBlocksAsync()
    {
        if (_artifactBlocks.Count == 0)
        {
            return Task.CompletedTask;
        }

        return Task.WhenAll(_artifactBlocks.Select(b => b.Completion));
    }

    internal void MarkArtifactBatcherFinalized() => _artifactBatcherFinalized = true;

    public PipelineContext(
        Guid runId,
        Type resourceType,
        CancellationToken cancellationToken,
        ILogger? logger = null,
        IPipelineProgressTracker? progressTracker = null,
        IArtifactBatcher? artifactBatcher = null,
        IResourceRunCache? resourceRunCache = null,
        PipelineStepOptions? defaultStepOptions = null,
        string? category = null,
        string? name = null,
        PipelineRunType runType = PipelineRunType.Fresh,
        Guid? parentRunId = null)
    {
        if (runId == Guid.Empty)
            throw new ArgumentException("RunId cannot be empty", nameof(runId));

        RunId = runId;
        ResourceType = resourceType ?? throw new ArgumentNullException(nameof(resourceType));
        Category = category ?? string.Empty;
        Name = name ?? string.Empty;
        CancellationToken = cancellationToken;
        Logger = logger ?? NullLogger.Instance;
        ProgressTracker = progressTracker;
        ArtifactBatcher = artifactBatcher;
        ResourceRunCache = resourceRunCache;
        RunType = runType;
        ParentRunId = parentRunId;
        _defaultStepOptions = (defaultStepOptions ?? new PipelineStepOptions()).Clone();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        try
        {
            // Finalize progress tracker if present
            if (ProgressTracker != null)
            {
                await ProgressTracker.FinalizeAsync();
            }

            // Dispose tracker if it implements IDisposable or IAsyncDisposable
            if (ProgressTracker is IAsyncDisposable asyncDisposableTracker)
            {
                await asyncDisposableTracker.DisposeAsync();
            }
            else if (ProgressTracker is IDisposable disposableTracker)
            {
                disposableTracker.Dispose();
            }

            if (ArtifactBatcher != null && !_artifactBatcherFinalized)
            {
                try
                {
                    await ArtifactBatcher.FinalizeAsync();
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to finalize artifact batcher during pipeline context disposal for run {RunId}", RunId);
                }
            }

            if (ArtifactBatcher is IAsyncDisposable asyncDisposableBatcher)
            {
                await asyncDisposableBatcher.DisposeAsync();
            }
            else if (ArtifactBatcher is IDisposable disposableBatcher)
            {
                disposableBatcher.Dispose();
            }
        }
        finally
        {
            _disposed = true;
        }
    }
}

