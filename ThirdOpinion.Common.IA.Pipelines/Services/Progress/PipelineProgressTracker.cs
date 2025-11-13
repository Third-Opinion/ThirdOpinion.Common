using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using ThirdOpinion.Common.IA.Pipelines.Models;
using ThirdOpinion.Common.IA.Pipelines.Progress;
using ThirdOpinion.Common.IA.Pipelines.Progress.Models;

namespace ThirdOpinion.Common.IA.Pipelines.Services.Progress;

/// <summary>
/// Storage-agnostic implementation of the pipeline progress tracker.
/// </summary>
public class PipelineProgressTracker : IPipelineProgressTracker, IDisposable
{
    private const int ResourceBatchSize = 50;
    private const int StepBatchSize = 100;
    private const int CompletionBatchSize = 100;
    private static readonly TimeSpan ResourceFlushInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan StepFlushInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan CompletionFlushInterval = TimeSpan.FromSeconds(3);

    private readonly IPipelineProgressService _progressService;
    private readonly IResourceRunCache _resourceRunCache;
    private readonly ILogger<PipelineProgressTracker> _logger;

    private readonly ConcurrentDictionary<string, ResourceProgressState> _resourceStates = new();
    private readonly ConcurrentDictionary<string, Guid> _resourceRunIds = new();

    private Channel<ResourceProgressUpdate>? _resourceStartChannel;
    private Channel<StepProgressUpdate>? _stepProgressChannel;
    private Channel<ResourceCompletionUpdate>? _completionChannel;

    private Task? _resourceConsumerTask;
    private Task? _stepConsumerTask;
    private Task? _completionConsumerTask;

    private Guid _runId;
    private CancellationToken _cancellationToken;
    private string _category = string.Empty;
    private string _name = string.Empty;
    private PipelineRunType _runType = PipelineRunType.Fresh;
    private Guid? _parentRunId;
    private bool _initialized;
    private bool _disposed;
    private bool _runCreated;

    public PipelineProgressTracker(
        IPipelineProgressService progressService,
        IResourceRunCache resourceRunCache,
        ILogger<PipelineProgressTracker> logger)
    {
        _progressService = progressService ?? throw new ArgumentNullException(nameof(progressService));
        _resourceRunCache = resourceRunCache ?? throw new ArgumentNullException(nameof(resourceRunCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    internal void ConfigureMetadata(PipelineRunMetadata metadata)
    {
        if (metadata == null)
            throw new ArgumentNullException(nameof(metadata));

        _category = metadata.Category;
        _name = metadata.Name;
        _runType = metadata.RunType;
        _parentRunId = metadata.ParentRunId;
    }

    public void Initialize(Guid runId, CancellationToken ct)
    {
        if (_initialized)
        {
            throw new InvalidOperationException("Tracker already initialized.");
        }

        _runId = runId;
        _cancellationToken = ct;

        EnsureRunRecordExists(runId, ct);
        _resourceStartChannel = Channel.CreateUnbounded<ResourceProgressUpdate>();
        _stepProgressChannel = Channel.CreateUnbounded<StepProgressUpdate>();
        _completionChannel = Channel.CreateUnbounded<ResourceCompletionUpdate>();

        _resourceConsumerTask = Task.Run(ProcessResourceStartsAsync, CancellationToken.None);
        _stepConsumerTask = Task.Run(ProcessStepProgressAsync, CancellationToken.None);
        _completionConsumerTask = Task.Run(ProcessCompletionsAsync, CancellationToken.None);

        _initialized = true;

        _logger.LogInformation("Pipeline progress tracker initialized for run {RunId} ({Category}/{Name})", runId, _category, _name);
    }

    public void RecordResourceStart(string resourceId, string resourceType)
    {
        EnsureInitialized();

        var resourceRunId = _resourceRunCache.GetOrCreateAsync(_runId, resourceId, resourceType, _cancellationToken)
            .GetAwaiter()
            .GetResult();

        var isNewRegistration = _resourceRunIds.TryAdd(resourceId, resourceRunId);
        var state = _resourceStates.GetOrAdd(resourceId, _ => new ResourceProgressState
        {
            ResourceId = resourceId,
            ResourceType = resourceType,
            Status = PipelineResourceStatus.Processing,
            StartTime = DateTime.UtcNow
        });

        state.ResourceType = resourceType;

        if (!isNewRegistration)
        {
            _logger.LogTrace("Resource {ResourceId} already registered for run {RunId}; skipping duplicate start.", resourceId, _runId);
            return;
        }

        state.Status = PipelineResourceStatus.Processing;
        state.StartTime = DateTime.UtcNow;
        state.EndTime = null;
        state.ErrorMessage = null;
        state.ErrorStep = null;

        var update = new ResourceProgressUpdate
        {
            ResourceRunId = resourceRunId,
            ResourceId = resourceId,
            ResourceType = resourceType,
            Status = PipelineResourceStatus.Processing,
            StartTime = state.StartTime
        };

        PostToChannel(_resourceStartChannel!, update);
    }

    public void RecordStepStart(string[] resourcePath, string stepName)
    {
        EnsureInitialized();
        if (resourcePath.Length == 0) throw new ArgumentException("Resource path cannot be empty", nameof(resourcePath));

        var resourceId = resourcePath[0];
        var resourceRunId = GetResourceRunId(resourceId)
            ?? throw new InvalidOperationException($"Resource run not registered for resource '{resourceId}'. Call RecordResourceStart first.");
        UpdateResourceStateStep(resourceId, stepName, PipelineStepStatus.InProgress);

        var update = new StepProgressUpdate
        {
            ResourceRunId = resourceRunId,
            StepName = stepName,
            Status = PipelineStepStatus.InProgress,
            StartTime = DateTime.UtcNow
        };

        PostToChannel(_stepProgressChannel!, update);
    }

    public void RecordStepComplete(string[] resourcePath, string stepName, int durationMs)
    {
        EnsureInitialized();
        if (resourcePath.Length == 0) throw new ArgumentException("Resource path cannot be empty", nameof(resourcePath));

        var resourceId = resourcePath[0];
        var resourceRunId = GetResourceRunId(resourceId)
            ?? throw new InvalidOperationException($"Resource run not registered for resource '{resourceId}'. Call RecordResourceStart first.");
        UpdateResourceStateStep(resourceId, stepName, PipelineStepStatus.Completed, durationMs);

        var update = new StepProgressUpdate
        {
            ResourceRunId = resourceRunId,
            StepName = stepName,
            Status = PipelineStepStatus.Completed,
            EndTime = DateTime.UtcNow,
            DurationMs = durationMs
        };

        PostToChannel(_stepProgressChannel!, update);
    }

    public void RecordStepFailed(string[] resourcePath, string stepName, int durationMs, string? errorMessage = null)
    {
        EnsureInitialized();
        if (resourcePath.Length == 0) throw new ArgumentException("Resource path cannot be empty", nameof(resourcePath));

        var resourceId = resourcePath[0];
        var resourceRunId = GetResourceRunId(resourceId)
            ?? throw new InvalidOperationException($"Resource run not registered for resource '{resourceId}'. Call RecordResourceStart first.");
        UpdateResourceStateStep(resourceId, stepName, PipelineStepStatus.Failed, durationMs, errorMessage);

        var update = new StepProgressUpdate
        {
            ResourceRunId = resourceRunId,
            StepName = stepName,
            Status = PipelineStepStatus.Failed,
            EndTime = DateTime.UtcNow,
            DurationMs = durationMs,
            ErrorMessage = errorMessage
        };

        PostToChannel(_stepProgressChannel!, update);
    }

    public void RecordResourceComplete(string resourceId, PipelineResourceStatus finalStatus, string? errorMessage = null, string? errorStep = null)
    {
        EnsureInitialized();

        var resourceRunId = GetResourceRunId(resourceId)
            ?? throw new InvalidOperationException($"Resource run not registered for resource '{resourceId}'. Call RecordResourceStart first.");
        if (_resourceStates.TryGetValue(resourceId, out var state))
        {
            state.Status = finalStatus;
            state.EndTime = DateTime.UtcNow;
            state.ErrorMessage = errorMessage;
            state.ErrorStep = errorStep;
        }

        var update = new ResourceCompletionUpdate
        {
            ResourceRunId = resourceRunId,
            FinalStatus = finalStatus,
            EndTime = DateTime.UtcNow,
            ErrorMessage = errorMessage,
            ErrorStep = errorStep
        };

        PostToChannel(_completionChannel!, update);
    }

    public async Task FinalizeAsync()
    {
        if (!_initialized || _disposed)
            return;

        _resourceStartChannel?.Writer.TryComplete();
        _stepProgressChannel?.Writer.TryComplete();
        _completionChannel?.Writer.TryComplete();

        var tasks = new List<Task>();
        if (_resourceConsumerTask != null) tasks.Add(_resourceConsumerTask);
        if (_stepConsumerTask != null) tasks.Add(_stepConsumerTask);
        if (_completionConsumerTask != null) tasks.Add(_completionConsumerTask);

        await Task.WhenAll(tasks).ConfigureAwait(false);

        try
        {
            LogProgressSummary();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log progress summary for run {RunId}", _runId);
        }

        _initialized = false;
        _logger.LogInformation("Pipeline progress tracker finalized for run {RunId}", _runId);
    }

    public PipelineSnapshot GetPipelineSnapshot()
    {
        var resources = _resourceStates.Values;
        var snapshot = new PipelineSnapshot
        {
            RunId = _runId,
            SnapshotTime = DateTime.UtcNow,
            TotalResources = resources.Count,
            CompletedResources = resources.Count(r => r.Status == PipelineResourceStatus.Completed),
            FailedResources = resources.Count(r => r.Status == PipelineResourceStatus.Failed),
            ProcessingResources = resources.Count(r => r.Status == PipelineResourceStatus.Processing)
        };

        return snapshot;
    }

    public void LogProgressSummary()
    {
        var snapshot = GetPipelineSnapshot();
        _logger.LogInformation(
            "Run {RunId} ({Category}/{Name}) - Total: {Total}, Completed: {Completed}, Failed: {Failed}, Processing: {Processing}",
            snapshot.RunId,
            _category,
            _name,
            snapshot.TotalResources,
            snapshot.CompletedResources,
            snapshot.FailedResources,
            snapshot.ProcessingResources);
    }

    public ResourceProgressState? GetResourceProgress(string resourceId)
    {
        _resourceStates.TryGetValue(resourceId, out var state);
        return state;
    }

    public IReadOnlyDictionary<string, ResourceProgressState> GetAllResources()
    {
        return _resourceStates;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _resourceStartChannel?.Writer.TryComplete();
        _stepProgressChannel?.Writer.TryComplete();
        _completionChannel?.Writer.TryComplete();
    }

    private Guid? GetResourceRunId(string resourceId)
    {
        return _resourceRunIds.TryGetValue(resourceId, out var id) ? id : null;
    }

    private void UpdateResourceStateStep(string resourceId, string stepName, PipelineStepStatus status, int? durationMs = null, string? errorMessage = null)
    {
        var state = _resourceStates.GetOrAdd(resourceId, _ => new ResourceProgressState
        {
            ResourceId = resourceId,
            Status = PipelineResourceStatus.Processing,
            StartTime = DateTime.UtcNow
        });

        var step = state.StepProgresses.FirstOrDefault(s => s.StepName == stepName);
        if (step == null)
        {
            step = new StepProgressMetrics { StepName = stepName };
            state.StepProgresses.Add(step);
        }

        step.Status = status;
        step.EndTime = DateTime.UtcNow;
        step.DurationMs = durationMs;
        step.ErrorMessage = errorMessage;
    }

    private void PostToChannel<T>(Channel<T> channel, T item)
    {
        if (!channel.Writer.TryWrite(item))
        {
            _ = channel.Writer.WriteAsync(item, _cancellationToken).AsTask();
        }
    }

    private async Task ProcessResourceStartsAsync()
    {
        if (_resourceStartChannel == null)
            return;

        var batch = new List<ResourceProgressUpdate>(ResourceBatchSize);
        var reader = _resourceStartChannel.Reader;

        while (true)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken);
            timeoutCts.CancelAfter(ResourceFlushInterval);

            try
            {
                if (await reader.WaitToReadAsync(timeoutCts.Token).ConfigureAwait(false))
                {
                    while (reader.TryRead(out var update))
                    {
                        batch.Add(update);

                        if (batch.Count >= ResourceBatchSize)
                        {
                            await FlushResourceStartsAsync(batch, "batch-size").ConfigureAwait(false);
                        }
                    }
                }
                else
                {
                    break;
                }
            }
            catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested && !_cancellationToken.IsCancellationRequested)
            {
                if (batch.Count > 0)
                {
                    await FlushResourceStartsAsync(batch, "interval").ConfigureAwait(false);
                }

                continue;
            }
            catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }

        while (reader.TryRead(out var pending))
        {
            batch.Add(pending);
        }

        if (batch.Count > 0)
        {
            await FlushResourceStartsAsync(batch, "final-drain").ConfigureAwait(false);
        }
    }

    private async Task FlushResourceStartsAsync(List<ResourceProgressUpdate> batch, string reason)
    {
        if (batch.Count == 0)
            return;

        try
        {
            _logger.LogDebug("Flushing {Count} resource start updates for run {RunId} due to {Reason}", batch.Count, _runId, reason);
            await _progressService.CreateResourceRunsBatchAsync(_runId, batch.ToArray(), _cancellationToken).ConfigureAwait(false);
            batch.Clear();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist resource start batch for run {RunId}", _runId);
            throw;
        }
    }

    private async Task ProcessStepProgressAsync()
    {
        if (_stepProgressChannel == null)
            return;

        var batch = new List<StepProgressUpdate>(StepBatchSize);
        var reader = _stepProgressChannel.Reader;

        while (true)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken);
            timeoutCts.CancelAfter(StepFlushInterval);

            try
            {
                if (await reader.WaitToReadAsync(timeoutCts.Token).ConfigureAwait(false))
                {
                    while (reader.TryRead(out var update))
                    {
                        batch.Add(update);

                        if (batch.Count >= StepBatchSize)
                        {
                            await FlushStepProgressAsync(batch, "batch-size").ConfigureAwait(false);
                        }
                    }
                }
                else
                {
                    break;
                }
            }
            catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested && !_cancellationToken.IsCancellationRequested)
            {
                if (batch.Count > 0)
                {
                    await FlushStepProgressAsync(batch, "interval").ConfigureAwait(false);
                }

                continue;
            }
            catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }

        while (reader.TryRead(out var pending))
        {
            batch.Add(pending);
        }

        if (batch.Count > 0)
        {
            await FlushStepProgressAsync(batch, "final-drain").ConfigureAwait(false);
        }
    }

    private async Task FlushStepProgressAsync(List<StepProgressUpdate> batch, string reason)
    {
        if (batch.Count == 0)
            return;

        try
        {
            _logger.LogDebug("Flushing {Count} step progress updates for run {RunId} due to {Reason}", batch.Count, _runId, reason);
            var deferred = await _progressService.UpdateStepProgressBatchAsync(_runId, batch.ToArray(), _cancellationToken).ConfigureAwait(false);
            batch.Clear();
            if (deferred.Count > 0 && _stepProgressChannel != null)
            {
                _logger.LogDebug("Requeueing {Count} step progress updates for run {RunId} because resource runs were not yet available.", deferred.Count, _runId);

                await Task.Delay(TimeSpan.FromMilliseconds(50), _cancellationToken).ConfigureAwait(false);

                foreach (var update in deferred)
                {
                    PostToChannel(_stepProgressChannel, update);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist step progress batch for run {RunId}", _runId);
            throw;
        }
    }

    private async Task ProcessCompletionsAsync()
    {
        if (_completionChannel == null)
            return;

        var batch = new List<ResourceCompletionUpdate>(CompletionBatchSize);
        var reader = _completionChannel.Reader;

        while (true)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken);
            timeoutCts.CancelAfter(CompletionFlushInterval);

            try
            {
                if (await reader.WaitToReadAsync(timeoutCts.Token).ConfigureAwait(false))
                {
                    while (reader.TryRead(out var update))
                    {
                        batch.Add(update);

                        if (batch.Count >= CompletionBatchSize)
                        {
                            await FlushCompletionsAsync(batch, "batch-size").ConfigureAwait(false);
                        }
                    }
                }
                else
                {
                    break;
                }
            }
            catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested && !_cancellationToken.IsCancellationRequested)
            {
                if (batch.Count > 0)
                {
                    await FlushCompletionsAsync(batch, "interval").ConfigureAwait(false);
                }

                continue;
            }
            catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }

        while (reader.TryRead(out var pending))
        {
            batch.Add(pending);
        }

        if (batch.Count > 0)
        {
            await FlushCompletionsAsync(batch, "final-drain").ConfigureAwait(false);
        }
    }

    private async Task FlushCompletionsAsync(List<ResourceCompletionUpdate> batch, string reason)
    {
        if (batch.Count == 0)
            return;

        try
        {
            _logger.LogDebug("Flushing {Count} resource completion updates for run {RunId} due to {Reason}", batch.Count, _runId, reason);
            await _progressService.CompleteResourceRunsBatchAsync(_runId, batch.ToArray(), _cancellationToken).ConfigureAwait(false);
            batch.Clear();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist resource completion batch for run {RunId}", _runId);
            throw;
        }
    }

    private void EnsureRunRecordExists(Guid runId, CancellationToken ct)
    {
        if (_runCreated)
        {
            return;
        }

        var request = new CreatePipelineRunRequest
        {
            RunId = runId,
            Category = string.IsNullOrWhiteSpace(_category) ? "Pipeline" : _category,
            Name = string.IsNullOrWhiteSpace(_name) ? runId.ToString("N") : _name,
            RunType = _runType,
            ParentRunId = _parentRunId,
            Config = new PipelineRunConfiguration
            {
                RunType = _runType,
                ParentRunId = _parentRunId
            }
        };

        _progressService.CreateRunAsync(request, ct).GetAwaiter().GetResult();
        _runCreated = true;
    }

    private void EnsureInitialized()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Progress tracker not initialized. Call Initialize first.");
        }
    }
}

