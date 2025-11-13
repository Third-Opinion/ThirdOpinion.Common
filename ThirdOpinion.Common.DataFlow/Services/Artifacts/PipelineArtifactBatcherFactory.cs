using Microsoft.Extensions.DependencyInjection;
using ThirdOpinion.Common.DataFlow.Artifacts;
using ThirdOpinion.Common.DataFlow.Progress.Models;

namespace ThirdOpinion.Common.DataFlow.Services.Artifacts;

/// <summary>
/// Factory that creates <see cref="PipelineArtifactBatcher"/> instances for each pipeline run.
/// </summary>
public class PipelineArtifactBatcherFactory : IArtifactBatcherFactory
{
    private readonly IServiceProvider _serviceProvider;

    public PipelineArtifactBatcherFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public IArtifactBatcher Create(PipelineRunMetadata metadata, CancellationToken cancellationToken)
    {
        if (metadata is null)
            throw new ArgumentNullException(nameof(metadata));

        var runId = metadata.GetOrCreateRunId();
        var batcher = _serviceProvider.GetRequiredService<PipelineArtifactBatcher>();
        batcher.Initialize(runId, cancellationToken);
        return batcher;
    }
}

