using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using ThirdOpinion.Common.DataFlow.Artifacts;
using ThirdOpinion.Common.DataFlow.Artifacts.Models;

namespace ThirdOpinion.Common.DataFlow.Services.EfCore;

/// <summary>
/// Entity Framework backed implementation of <see cref="IArtifactBatcher"/> that writes artifacts to the database.
/// </summary>
public class EfArtifactBatcher : IArtifactBatcher
{
    private readonly IArtifactStorageService _storageService;
    private readonly ILogger<EfArtifactBatcher> _logger;
    private readonly int _batchSize;
    private readonly TimeSpan _flushInterval;

    private Guid _runId;
    private CancellationToken _cancellationToken;
    private Channel<ArtifactSaveRequest>? _channel;
    private Task? _consumerTask;
    private readonly TaskCompletionSource _consumerStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource> _completionTokens = new();

    public EfArtifactBatcher(
        IArtifactStorageService storageService,
        ILogger<EfArtifactBatcher> logger,
        int batchSize = 100,
        TimeSpan? flushInterval = null)
    {
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _batchSize = batchSize;
        _flushInterval = flushInterval ?? TimeSpan.FromSeconds(2);
    }

    public void Initialize(Guid runId, CancellationToken ct)
    {
        if (_channel != null)
        {
            throw new InvalidOperationException("Artifact batcher already initialized.");
        }

        _runId = runId;
        _cancellationToken = ct;
        _channel = Channel.CreateUnbounded<ArtifactSaveRequest>();
        _consumerTask = Task.Run(ProcessArtifactsAsync, CancellationToken.None);

        _logger.LogDebug("Artifact batcher initialized for pipeline run {RunId}", runId);
    }

    public async Task QueueArtifactSaveAsync(ArtifactSaveRequest request, CancellationToken ct = default)
    {
        if (_channel == null)
        {
            throw new InvalidOperationException("Artifact batcher not initialized. Call Initialize first.");
        }

        var tokenId = Guid.NewGuid();
        var completionToken = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_completionTokens.TryAdd(tokenId, completionToken))
        {
            throw new InvalidOperationException($"Duplicate artifact completion token generated: {tokenId}");
        }

        request.CompletionTokenId = tokenId;

        _logger.LogTrace("Queued artifact {Step}/{Artifact} for run {RunId}", request.StepName, request.ArtifactName, _runId);

        await _channel.Writer.WriteAsync(request, ct);
    }

    public async Task FinalizeAsync()
    {
        if (_channel == null || _consumerTask == null)
        {
            return;
        }

        await _consumerStarted.Task.ConfigureAwait(false);
        _channel.Writer.Complete();

        await _consumerTask.ConfigureAwait(false);

        var pendingTokens = _completionTokens.Where(kvp => !kvp.Value.Task.IsCompleted).Select(kvp => kvp.Key).ToList();
        if (pendingTokens.Count > 0)
        {
            _logger.LogWarning("Artifact batcher finalized with {Count} pending completion tokens. Forcing completion.",
                pendingTokens.Count);

            foreach (var tokenId in pendingTokens)
            {
                if (_completionTokens.TryGetValue(tokenId, out var tcs))
                {
                    tcs.TrySetResult();
                }
            }
        }

        _logger.LogInformation("Artifact batcher finalized for run {RunId}", _runId);
    }

    private async Task ProcessArtifactsAsync()
    {
        if (_channel == null)
            return;

        _consumerStarted.TrySetResult();

        var batch = new List<ArtifactSaveRequest>(_batchSize);
        var lastFlush = DateTime.UtcNow;
        var reader = _channel.Reader;

        try
        {
            while (await reader.WaitToReadAsync(_cancellationToken).ConfigureAwait(false))
            {
                while (reader.TryRead(out var request))
                {
                    batch.Add(request);

                    if (batch.Count >= _batchSize || DateTime.UtcNow - lastFlush >= _flushInterval)
                    {
                        await FlushAsync(batch).ConfigureAwait(false);
                        batch.Clear();
                        lastFlush = DateTime.UtcNow;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Artifact batcher cancelled for run {RunId}", _runId);
        }
        finally
        {
            while (reader.TryRead(out var request))
            {
                batch.Add(request);
            }

            if (batch.Count > 0)
            {
                await FlushAsync(batch).ConfigureAwait(false);
            }
        }
    }

    private async Task FlushAsync(List<ArtifactSaveRequest> batch)
    {
        if (batch.Count == 0)
            return;

        try
        {
            _logger.LogDebug("Persisting {Count} artifacts for run {RunId}", batch.Count, _runId);
            await _storageService.SaveBatchAsync(batch, _cancellationToken).ConfigureAwait(false);

            foreach (var request in batch)
            {
                if (_completionTokens.TryGetValue(request.CompletionTokenId, out var tcs))
                {
                    tcs.TrySetResult();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist {Count} artifacts for run {RunId}", batch.Count, _runId);
            foreach (var request in batch)
            {
                if (_completionTokens.TryGetValue(request.CompletionTokenId, out var tcs))
                {
                    tcs.TrySetException(ex);
                }
            }
        }
    }
}


