using System.Collections.Concurrent;
using System.Threading.Channels;
using ThirdOpinion.Common.IA.Pipelines.Artifacts;
using ThirdOpinion.Common.IA.Pipelines.Artifacts.Models;

namespace ThirdOpinion.Common.IA.Pipelines.Services.InMemory;

/// <summary>
/// In-memory implementation of IArtifactBatcher for testing
/// Uses Channel for queuing and processes batches asynchronously
/// Uses TaskCompletionSource tokens to track artifact persistence (matches production ArtifactBatcher pattern)
/// </summary>
public class InMemoryArtifactBatcher : IArtifactBatcher
{
    private readonly IArtifactStorageService _storageService;
    private readonly int _batchSize;
    private readonly int _flushIntervalMs;
    
    private Guid _runId;
    private CancellationToken _cancellationToken;
    private Channel<ArtifactSaveRequest>? _channel;
    private Task? _processingTask;
    private readonly TaskCompletionSource _processingStarted = new();
    
    // Track completion tokens for each queued artifact (matches ProgressTracker pattern)
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource> _completionTokens = new();

    public InMemoryArtifactBatcher(
        IArtifactStorageService storageService,
        int batchSize = 100,
        int flushIntervalMs = 1000)
    {
        _storageService = storageService;
        _batchSize = batchSize;
        _flushIntervalMs = flushIntervalMs;
    }

    public void Initialize(Guid runId, CancellationToken ct)
    {
        _runId = runId;
        _cancellationToken = ct;
        _channel = Channel.CreateUnbounded<ArtifactSaveRequest>();
        _processingTask = ProcessBatchesAsync();
    }

    public async Task QueueArtifactSaveAsync(ArtifactSaveRequest request, CancellationToken ct = default)
    {
        if (_channel == null)
            throw new InvalidOperationException("Batcher not initialized. Call Initialize first.");

        // Create completion token for this artifact (matches ProgressTracker.RecordResourceComplete pattern)
        var tokenId = Guid.NewGuid();
        var completionToken = new TaskCompletionSource();
        if (!_completionTokens.TryAdd(tokenId, completionToken))
        {
            throw new InvalidOperationException($"Duplicate completion token id detected: {tokenId}");
        }
        
        // Attach token ID to request so it can be signaled after persistence
        request.CompletionTokenId = tokenId;

        await _channel.Writer.WriteAsync(request, ct);
    }

    public async Task FinalizeAsync()
    {
        if (_channel == null || _processingTask == null)
            return;

        // CRITICAL: Wait for processing task to actually start before completing the channel
        // This prevents a race where the channel completes before ProcessBatchesAsync enters the ReadAllAsync loop
        await _processingStarted.Task;

        // Complete channel to signal no more items (matches ArtifactBatcher.cs line 114)
        _channel.Writer.Complete();

        // CRITICAL: Wait for processing task FIRST to ensure all items are drained and flushed
        // Only after processing completes can we safely check completion tokens
        await _processingTask;

        // Now verify all artifact completion tokens were signaled (matches ProgressTracker.FinalizeAsync pattern lines 369-428)
        var completionTasks = _completionTokens.Values.Select(tcs => tcs.Task).ToArray();
        if (completionTasks.Any())
        {
            var stillPending = completionTasks.Where(t => !t.IsCompleted).ToArray();
            if (stillPending.Any())
            {
                // Some tokens weren't signaled - this indicates a bug in the batching logic
                var unsignaledIds = _completionTokens
                    .Where(kvp => !kvp.Value.Task.IsCompleted)
                    .Select(kvp => kvp.Key)
                    .Take(10)
                    .ToArray();

                throw new InvalidOperationException(
                    $"After processing completed, {stillPending.Length} artifact completion tokens were not signaled. " +
                    $"This indicates artifacts were queued but never flushed. " +
                    $"Sample unsignaled token IDs: {string.Join(", ", unsignaledIds)}");
            }
        }
    }

    private async Task ProcessBatchesAsync()
    {
        if (_channel == null)
            return;

        var batch = new List<ArtifactSaveRequest>();
        var lastFlushTime = DateTime.UtcNow;

        // Signal that processing has started before entering the loop
        _processingStarted.TrySetResult();

        var reader = _channel.Reader;

        try
        {
            // Use WaitToReadAsync pattern from production ArtifactBatcher (lines 238-306)
            // This is more robust than ReadAllAsync for ensuring all items are processed
            while (true)
            {
                // Check if channel is completed explicitly
                if (reader.Completion.IsCompleted)
                {
                    break;
                }

                // Wait for data with timeout to periodically check completion
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(1));

                bool dataAvailable;
                try
                {
                    dataAvailable = await reader.WaitToReadAsync(timeoutCts.Token);
                }
                catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested && !_cancellationToken.IsCancellationRequested)
                {
                    // Timeout - check if channel is complete
                    if (reader.Completion.IsCompleted)
                    {
                        break;
                    }
                    continue;
                }

                if (!dataAvailable)
                {
                    break;
                }

                // Read all available items
                while (reader.TryRead(out var request))
                {
                    batch.Add(request);

                    // Flush if batch is full or flush interval elapsed
                    var timeSinceLastFlush = DateTime.UtcNow - lastFlushTime;
                    if (batch.Count >= _batchSize || timeSinceLastFlush.TotalMilliseconds >= _flushIntervalMs)
                    {
                        await FlushBatchAsync(batch);
                        batch.Clear();
                        lastFlushTime = DateTime.UtcNow;
                    }
                }
            }

            // CRITICAL: Drain any remaining items from channel (matches production ArtifactBatcher lines 313-326)
            var drainedCount = 0;
            while (reader.TryRead(out var request))
            {
                batch.Add(request);
                drainedCount++;
            }

            // Flush remaining items (including drained)
            if (batch.Count > 0)
            {
                await FlushBatchAsync(batch);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on cancellation - flush remaining
            if (batch.Count > 0)
            {
                await FlushBatchAsync(batch);
            }
        }
    }

    private async Task FlushBatchAsync(List<ArtifactSaveRequest> batch)
    {
        if (batch.Count == 0)
            return;

        try
        {
            // Persist artifacts to storage
            await _storageService.SaveBatchAsync(batch, _cancellationToken);

            // CRITICAL: Signal completion tokens AFTER successful persistence (matches ProgressTracker.FlushResourceCompletionBatch lines 839-861)
            foreach (var request in batch)
            {
                if (_completionTokens.TryGetValue(request.CompletionTokenId, out var tcs))
                {
                    tcs.TrySetResult();
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Completion token {request.CompletionTokenId} was not registered for artifact {request.StepName}/{request.ArtifactName}.");
                }
            }
        }
        catch (Exception ex)
        {
            // Signal completion tokens even on failure to prevent deadlock (matches ProgressTracker lines 874-889)
            foreach (var request in batch)
            {
                if (_completionTokens.TryGetValue(request.CompletionTokenId, out var tcs))
                {
                    tcs.TrySetException(ex); // Signal with exception
                }
            }
            
            // Log failures but don't throw - artifact failures shouldn't crash the batcher
            // In production, this would be logged properly
        }
    }
}

