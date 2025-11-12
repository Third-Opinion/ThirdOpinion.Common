using ThirdOpinion.Common.DataFlow.Models;
using ThirdOpinion.Common.DataFlow.Progress;
using ThirdOpinion.Common.DataFlow.Progress.Models;

namespace ThirdOpinion.Common.DataFlow.Examples;

/// <summary>
/// Example custom implementation of IPipelineProgressTracker
/// This example tracks progress and writes to console with colors
/// </summary>
public class CustomProgressTracker : IPipelineProgressTracker
{
    private Guid _runId;
    private readonly Dictionary<string, ResourceProgressState> _resources = new();
    private readonly object _lock = new();
    private int _completedCount = 0;
    private int _failedCount = 0;

    public void Initialize(Guid runId, CancellationToken ct)
    {
        _runId = runId;
        Console.WriteLine($"[Tracker] Initialized for run {runId:N}");
    }

    public void RecordResourceStart(string resourceId, string resourceType)
    {
        lock (_lock)
        {
            if (!_resources.ContainsKey(resourceId))
            {
                _resources[resourceId] = new ResourceProgressState
                {
                    ResourceId = resourceId,
                    ResourceType = resourceType,
                    Status = PipelineResourceStatus.Processing,
                    StartTime = DateTime.UtcNow,
                    StepProgresses = new List<StepProgressMetrics>()
                };

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"[Tracker] Started resource: {resourceId}");
                Console.ResetColor();
            }
        }
    }

    public void RecordStepStart(string[] resourcePath, string stepName)
    {
        var resourceId = resourcePath[0];
        lock (_lock)
        {
            if (_resources.TryGetValue(resourceId, out var state))
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

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[Tracker]   Step started: {resourceId} -> {stepName}");
                Console.ResetColor();
            }
        }
    }

    public void RecordStepComplete(string[] resourcePath, string stepName, int durationMs)
    {
        var resourceId = resourcePath[0];
        lock (_lock)
        {
            if (_resources.TryGetValue(resourceId, out var state))
            {
                var step = state.StepProgresses.FirstOrDefault(s => s.StepName == stepName);
                if (step != null)
                {
                    step.Status = PipelineStepStatus.Completed;
                    step.EndTime = DateTime.UtcNow;
                    step.DurationMs = durationMs;

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[Tracker]   Step completed: {resourceId} -> {stepName} ({durationMs}ms)");
                    Console.ResetColor();
                }
            }
        }
    }

    public void RecordStepFailed(string[] resourcePath, string stepName, int durationMs, string? errorMessage = null)
    {
        var resourceId = resourcePath[0];
        lock (_lock)
        {
            if (_resources.TryGetValue(resourceId, out var state))
            {
                var step = state.StepProgresses.FirstOrDefault(s => s.StepName == stepName);
                if (step != null)
                {
                    step.Status = PipelineStepStatus.Failed;
                    step.EndTime = DateTime.UtcNow;
                    step.DurationMs = durationMs;
                    step.ErrorMessage = errorMessage;

                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[Tracker]   Step failed: {resourceId} -> {stepName}: {errorMessage}");
                    Console.ResetColor();
                }
            }
        }
    }

    public void RecordResourceComplete(string resourceId, PipelineResourceStatus finalStatus, 
        string? errorMessage = null, string? errorStep = null)
    {
        lock (_lock)
        {
            if (_resources.TryGetValue(resourceId, out var state))
            {
                state.Status = finalStatus;
                state.EndTime = DateTime.UtcNow;
                state.ErrorMessage = errorMessage;
                state.ErrorStep = errorStep;

                if (finalStatus == PipelineResourceStatus.Completed)
                {
                    _completedCount++;
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[Tracker] ✓ Resource completed: {resourceId}");
                    Console.ResetColor();
                }
                else if (finalStatus == PipelineResourceStatus.Failed)
                {
                    _failedCount++;
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[Tracker] ✗ Resource failed: {resourceId} - {errorMessage}");
                    Console.ResetColor();
                }
            }
        }
    }

    public Task FinalizeAsync()
    {
        Console.WriteLine("\n" + new string('=', 60));
        Console.WriteLine($"[Tracker] Pipeline run {_runId:N} finalized");
        Console.WriteLine($"  Total resources: {_resources.Count}");
        Console.WriteLine($"  Completed: {_completedCount}");
        Console.WriteLine($"  Failed: {_failedCount}");
        Console.WriteLine(new string('=', 60));
        return Task.CompletedTask;
    }

    public PipelineSnapshot GetPipelineSnapshot()
    {
        lock (_lock)
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
    }

    public void LogProgressSummary()
    {
        var snapshot = GetPipelineSnapshot();
        Console.WriteLine($"\n[Progress] {snapshot.CompletedResources}/{snapshot.TotalResources} completed, " +
                         $"{snapshot.FailedResources} failed, {snapshot.ProcessingResources} in progress");
    }
}

