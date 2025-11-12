using System.Collections.Concurrent;
using ThirdOpinion.Common.DataFlow.Models;
using ThirdOpinion.Common.DataFlow.Progress;
using ThirdOpinion.Common.DataFlow.Progress.Models;

namespace ThirdOpinion.Common.DataFlow.Services.InMemory;

/// <summary>
/// In-memory implementation of IPipelineProgressTracker for testing and simple scenarios
/// Thread-safe implementation using ConcurrentDictionary
/// </summary>
public class InMemoryProgressTracker : IPipelineProgressTracker
{
    private Guid _runId;
    private CancellationToken _cancellationToken;
    private readonly ConcurrentDictionary<string, ResourceProgressState> _resources = new();
    private int _completedCount = 0;
    private int _failedCount = 0;

    public void Initialize(Guid runId, CancellationToken ct)
    {
        _runId = runId;
        _cancellationToken = ct;
    }

    public void RecordResourceStart(string resourceId, string resourceType)
    {
        _resources.TryAdd(resourceId, new ResourceProgressState
        {
            ResourceId = resourceId,
            ResourceType = resourceType,
            Status = PipelineResourceStatus.Processing,
            StartTime = DateTime.UtcNow,
            StepProgresses = new List<StepProgressMetrics>()
        });
    }

    public void RecordStepStart(string[] resourcePath, string stepName)
    {
        var resourceId = resourcePath[0];
        if (_resources.TryGetValue(resourceId, out var state))
        {
            lock (state.StepProgresses)
            {
                var existingStep = state.StepProgresses.FirstOrDefault(s => s.StepName == stepName);
                if (existingStep != null)
                {
                    existingStep.Status = PipelineStepStatus.InProgress;
                    existingStep.StartTime = DateTime.UtcNow;
                }
                else
                {
                    state.StepProgresses.Add(new StepProgressMetrics
                    {
                        StepName = stepName,
                        Status = PipelineStepStatus.InProgress,
                        StartTime = DateTime.UtcNow,
                        Sequence = state.StepProgresses.Count
                    });
                }
            }
        }
    }

    public void RecordStepComplete(string[] resourcePath, string stepName, int durationMs)
    {
        var resourceId = resourcePath[0];
        if (_resources.TryGetValue(resourceId, out var state))
        {
            lock (state.StepProgresses)
            {
                var step = state.StepProgresses.FirstOrDefault(s => s.StepName == stepName);
                if (step != null)
                {
                    step.Status = PipelineStepStatus.Completed;
                    step.EndTime = DateTime.UtcNow;
                    step.DurationMs = durationMs;
                }
            }
        }
    }

    public void RecordStepFailed(string[] resourcePath, string stepName, int durationMs, string? errorMessage = null)
    {
        var resourceId = resourcePath[0];
        if (_resources.TryGetValue(resourceId, out var state))
        {
            lock (state.StepProgresses)
            {
                var step = state.StepProgresses.FirstOrDefault(s => s.StepName == stepName);
                if (step != null)
                {
                    step.Status = PipelineStepStatus.Failed;
                    step.EndTime = DateTime.UtcNow;
                    step.DurationMs = durationMs;
                    step.ErrorMessage = errorMessage;
                }
            }
        }
    }

    public void RecordResourceComplete(string resourceId, PipelineResourceStatus finalStatus, 
        string? errorMessage = null, string? errorStep = null)
    {
        if (_resources.TryGetValue(resourceId, out var state))
        {
            state.Status = finalStatus;
            state.EndTime = DateTime.UtcNow;
            state.ErrorMessage = errorMessage;
            state.ErrorStep = errorStep;

            if (finalStatus == PipelineResourceStatus.Completed)
            {
                Interlocked.Increment(ref _completedCount);
            }
            else if (finalStatus == PipelineResourceStatus.Failed)
            {
                Interlocked.Increment(ref _failedCount);
            }
        }
    }

    public Task FinalizeAsync()
    {
        // In-memory tracker doesn't need to flush to storage
        return Task.CompletedTask;
    }

    public PipelineSnapshot GetPipelineSnapshot()
    {
        return new PipelineSnapshot
        {
            RunId = _runId,
            SnapshotTime = DateTime.UtcNow,
            TotalResources = _resources.Count,
            CompletedResources = _completedCount,
            FailedResources = _failedCount,
            ProcessingResources = _resources.Count - _completedCount - _failedCount
        };
    }

    public void LogProgressSummary()
    {
        var snapshot = GetPipelineSnapshot();
        Console.WriteLine($"Progress: {snapshot.CompletedResources}/{snapshot.TotalResources} completed, " +
                         $"{snapshot.FailedResources} failed, {snapshot.ProcessingResources} in progress");
    }

    /// <summary>
    /// Get all resource states (for testing/debugging)
    /// </summary>
    public IReadOnlyDictionary<string, ResourceProgressState> GetAllResourceStates()
    {
        return _resources;
    }
}

