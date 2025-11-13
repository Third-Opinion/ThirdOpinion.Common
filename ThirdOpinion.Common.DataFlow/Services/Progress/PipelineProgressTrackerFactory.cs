using Microsoft.Extensions.DependencyInjection;
using ThirdOpinion.Common.DataFlow.Progress;
using ThirdOpinion.Common.DataFlow.Progress.Models;

namespace ThirdOpinion.Common.DataFlow.Services.Progress;

/// <summary>
/// Factory that creates <see cref="PipelineProgressTracker"/> instances and configures metadata.
/// </summary>
public class PipelineProgressTrackerFactory : IPipelineProgressTrackerFactory
{
    private readonly IServiceProvider _serviceProvider;

    public PipelineProgressTrackerFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public IPipelineProgressTracker Create(PipelineRunMetadata metadata, CancellationToken cancellationToken)
    {
        if (metadata is null)
            throw new ArgumentNullException(nameof(metadata));

        var tracker = _serviceProvider.GetRequiredService<PipelineProgressTracker>();
        tracker.ConfigureMetadata(metadata);
        tracker.Initialize(metadata.GetOrCreateRunId(), cancellationToken);
        return tracker;
    }
}

