using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ThirdOpinion.Common.IA.Pipelines.Artifacts;
using ThirdOpinion.Common.IA.Pipelines.Progress;

namespace ThirdOpinion.Common.IA.Pipelines.Core;

/// <summary>
/// Default implementation of IPipelineContextFactory that resolves dependencies
/// </summary>
public class PipelineContextFactory : IPipelineContextFactory
{
    private readonly IPipelineProgressTrackerFactory? _progressTrackerFactory;
    private readonly IArtifactBatcherFactory? _artifactBatcherFactory;
    private readonly IResourceRunCache? _resourceRunCache;
    private readonly ILogger<PipelineContextFactory> _logger;

    public PipelineContextFactory(
        ILogger<PipelineContextFactory>? logger = null,
        IPipelineProgressTrackerFactory? progressTrackerFactory = null,
        IArtifactBatcherFactory? artifactBatcherFactory = null,
        IResourceRunCache? resourceRunCache = null)
    {
        _logger = logger ?? NullLogger<PipelineContextFactory>.Instance;
        _progressTrackerFactory = progressTrackerFactory;
        _artifactBatcherFactory = artifactBatcherFactory;
        _resourceRunCache = resourceRunCache;
    }

    public PipelineContextBuilder CreateBuilder<TResource>()
    {
        return CreateBuilder(typeof(TResource));
    }

    public PipelineContextBuilder CreateBuilder(Type resourceType)
    {
        return new PipelineContextBuilder(
            resourceType,
            _progressTrackerFactory,
            _artifactBatcherFactory,
            _resourceRunCache,
            _logger);
    }
}

