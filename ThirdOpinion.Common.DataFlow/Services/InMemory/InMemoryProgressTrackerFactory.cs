using ThirdOpinion.Common.DataFlow.Progress;
using ThirdOpinion.Common.DataFlow.Progress.Models;

namespace ThirdOpinion.Common.DataFlow.Services.InMemory;

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

