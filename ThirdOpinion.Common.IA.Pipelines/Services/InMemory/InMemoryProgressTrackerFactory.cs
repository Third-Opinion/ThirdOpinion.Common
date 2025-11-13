using ThirdOpinion.Common.IA.Pipelines.Progress;
using ThirdOpinion.Common.IA.Pipelines.Progress.Models;

namespace ThirdOpinion.Common.IA.Pipelines.Services.InMemory;

/// <summary>
/// Factory for creating in-memory progress trackers
/// </summary>
public class InMemoryProgressTrackerFactory : IPipelineProgressTrackerFactory
{
    public IPipelineProgressTracker Create(PipelineRunMetadata metadata, CancellationToken cancellationToken)
    {
        var tracker = new InMemoryProgressTracker();
        tracker.Initialize(metadata.GetOrCreateRunId(), cancellationToken);
        return tracker;
    }
}

