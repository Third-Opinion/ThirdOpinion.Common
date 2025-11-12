using Microsoft.Extensions.DependencyInjection;
using ThirdOpinion.Common.DataFlow.Models;
using ThirdOpinion.Common.DataFlow.Progress;
using ThirdOpinion.Common.DataFlow.Progress.Models;

namespace ThirdOpinion.Common.DataFlow.Services.EfCore;

/// <summary>
/// Factory for creating EF Core backed pipeline progress trackers.
/// </summary>
public class EfPipelineProgressTrackerFactory : IPipelineProgressTrackerFactory
{
    private readonly IServiceProvider _serviceProvider;

    public EfPipelineProgressTrackerFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public IPipelineProgressTracker Create(PipelineRunMetadata metadata, CancellationToken cancellationToken)
    {
        if (metadata is null)
            throw new ArgumentNullException(nameof(metadata));

        var runId = metadata.GetOrCreateRunId();
        var progressService = _serviceProvider.GetRequiredService<IPipelineProgressService>();

        progressService.CreateRunAsync(new CreatePipelineRunRequest
        {
            RunId = runId,
            Category = metadata.Category ?? string.Empty,
            Name = metadata.Name ?? string.Empty,
            RunType = PipelineRunType.Fresh
        }, cancellationToken).GetAwaiter().GetResult();

        var tracker = _serviceProvider.GetRequiredService<EfPipelineProgressTracker>();
        tracker.ConfigureMetadata(metadata.Category ?? string.Empty, metadata.Name ?? string.Empty);
        tracker.Initialize(runId, cancellationToken);
        return tracker;
    }
}


