using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ThirdOpinion.Common.DataFlow.Core;

namespace ThirdOpinion.Common.DataFlow.Services.InMemory;

/// <summary>
/// Factory for creating pre-configured in-memory services
/// Useful for testing and simple scenarios
/// </summary>
public static class InMemoryServiceFactory
{
    /// <summary>
    /// Create a complete pipeline context with all in-memory services
    /// </summary>
    public static IPipelineContext CreateContextWithAllServices(
        Type resourceType,
        string category,
        string name,
        CancellationToken cancellationToken,
        Guid? runId = null,
        ILogger? logger = null)
    {
        var factory = new InMemoryProgressTrackerFactory();
        var artifactStorage = new InMemoryArtifactStorageService();
        var artifactBatcherFactory = new InMemoryArtifactBatcherFactory(artifactStorage);
        var resourceCache = new InMemoryResourceRunCache();

        var builder = new PipelineContextBuilder(
            resourceType,
            factory,
            artifactBatcherFactory,
            resourceCache,
            logger ?? NullLogger.Instance);

        return builder
            .WithCategory(category)
            .WithName(name)
            .WithRunId(runId ?? Guid.NewGuid())
            .WithCancellationToken(cancellationToken)
            .Build();
    }
    
    /// <summary>
    /// Create a complete pipeline context with all in-memory services using generic type parameter
    /// </summary>
    public static IPipelineContext CreateContextWithAllServices<TResource>(
        string category,
        string name,
        CancellationToken cancellationToken,
        Guid? runId = null,
        ILogger? logger = null)
    {
        return CreateContextWithAllServices(typeof(TResource), category, name, cancellationToken, runId, logger);
    }

    /// <summary>
    /// Create a minimal pipeline context (no optional services)
    /// </summary>
    public static IPipelineContext CreateMinimalContext(
        Type resourceType,
        string category,
        string name,
        CancellationToken cancellationToken,
        Guid? runId = null,
        ILogger? logger = null)
    {
        var builder = new PipelineContextBuilder(
            resourceType,
            progressTrackerFactory: null,
            artifactBatcherFactory: null,
            resourceRunCache: null,
            logger ?? NullLogger.Instance);

        return builder
            .WithCategory(category)
            .WithName(name)
            .WithRunId(runId ?? Guid.NewGuid())
            .WithCancellationToken(cancellationToken)
            .Build();
    }
    
    /// <summary>
    /// Create a minimal pipeline context using generic type parameter
    /// </summary>
    public static IPipelineContext CreateMinimalContext<TResource>(
        string category,
        string name,
        CancellationToken cancellationToken,
        Guid? runId = null,
        ILogger? logger = null)
    {
        return CreateMinimalContext(typeof(TResource), category, name, cancellationToken, runId, logger);
    }

    /// <summary>
    /// Create a context with progress tracking only
    /// </summary>
    public static IPipelineContext CreateContextWithProgress(
        Type resourceType,
        string category,
        string name,
        CancellationToken cancellationToken,
        Guid? runId = null,
        ILogger? logger = null)
    {
        var factory = new InMemoryProgressTrackerFactory();

        var builder = new PipelineContextBuilder(
            resourceType,
            factory,
            artifactBatcherFactory: null,
            resourceRunCache: null,
            logger ?? NullLogger.Instance);

        return builder
            .WithCategory(category)
            .WithName(name)
            .WithRunId(runId ?? Guid.NewGuid())
            .WithCancellationToken(cancellationToken)
            .Build();
    }
    
    /// <summary>
    /// Create a context with progress tracking only using generic type parameter
    /// </summary>
    public static IPipelineContext CreateContextWithProgress<TResource>(
        string category,
        string name,
        CancellationToken cancellationToken,
        Guid? runId = null,
        ILogger? logger = null)
    {
        return CreateContextWithProgress(typeof(TResource), category, name, cancellationToken, runId, logger);
    }

    /// <summary>
    /// Create a context with artifact storage only
    /// </summary>
    public static IPipelineContext CreateContextWithArtifacts(
        Type resourceType,
        string category,
        string name,
        CancellationToken cancellationToken,
        Guid? runId = null,
        ILogger? logger = null)
    {
        var artifactStorage = new InMemoryArtifactStorageService();
        var artifactBatcherFactory = new InMemoryArtifactBatcherFactory(artifactStorage);

        var builder = new PipelineContextBuilder(
            resourceType,
            progressTrackerFactory: null,
            artifactBatcherFactory,
            resourceRunCache: null,
            logger ?? NullLogger.Instance);

        return builder
            .WithCategory(category)
            .WithName(name)
            .WithRunId(runId ?? Guid.NewGuid())
            .WithCancellationToken(cancellationToken)
            .Build();
    }
    
    /// <summary>
    /// Create a context with artifact storage only using generic type parameter
    /// </summary>
    public static IPipelineContext CreateContextWithArtifacts<TResource>(
        string category,
        string name,
        CancellationToken cancellationToken,
        Guid? runId = null,
        ILogger? logger = null)
    {
        return CreateContextWithArtifacts(typeof(TResource), category, name, cancellationToken, runId, logger);
    }

    /// <summary>
    /// Create standalone in-memory progress tracker
    /// </summary>
    public static InMemoryProgressTracker CreateProgressTracker()
    {
        return new InMemoryProgressTracker();
    }

    /// <summary>
    /// Create standalone in-memory artifact storage
    /// </summary>
    public static InMemoryArtifactStorageService CreateArtifactStorage()
    {
        return new InMemoryArtifactStorageService();
    }

    /// <summary>
    /// Create standalone in-memory artifact batcher
    /// </summary>
    public static InMemoryArtifactBatcher CreateArtifactBatcher(
        InMemoryArtifactStorageService? storageService = null,
        int batchSize = 100,
        int flushIntervalMs = 1000)
    {
        storageService ??= new InMemoryArtifactStorageService();
        return new InMemoryArtifactBatcher(storageService, batchSize, flushIntervalMs);
    }

    /// <summary>
    /// Create standalone in-memory resource run cache
    /// </summary>
    public static InMemoryResourceRunCache CreateResourceCache()
    {
        return new InMemoryResourceRunCache();
    }
}

