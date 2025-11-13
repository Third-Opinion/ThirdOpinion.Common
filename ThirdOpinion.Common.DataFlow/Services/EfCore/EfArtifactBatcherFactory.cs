using Microsoft.Extensions.DependencyInjection;
using ThirdOpinion.Common.DataFlow.Artifacts;
using ThirdOpinion.Common.DataFlow.Progress.Models;

namespace ThirdOpinion.Common.DataFlow.Services.EfCore;

/// <summary>
/// Factory that creates <see cref="EfArtifactBatcher"/> instances for each pipeline run.
/// </summary>
public class EfArtifactBatcherFactory : IArtifactBatcherFactory
{
    private readonly IServiceProvider _serviceProvider;

    public EfArtifactBatcherFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public IArtifactBatcher Create(PipelineRunMetadata metadata, CancellationToken cancellationToken)
    {
        if (metadata is null)
            throw new ArgumentNullException(nameof(metadata));

        var runId = metadata.GetOrCreateRunId();
        var batcher = _serviceProvider.GetRequiredService<EfArtifactBatcher>();
        batcher.Initialize(runId, cancellationToken);
        return batcher;
    }
}


