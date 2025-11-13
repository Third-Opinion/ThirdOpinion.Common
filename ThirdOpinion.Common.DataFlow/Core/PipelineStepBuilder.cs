using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;
using ThirdOpinion.Common.DataFlow.Blocks;
using ThirdOpinion.Common.DataFlow.Models;
using ThirdOpinion.Common.DataFlow.Results;

namespace ThirdOpinion.Common.DataFlow.Core;

/// <summary>
/// Builder for chaining pipeline steps
/// </summary>
/// <typeparam name="TIn">Input type of the pipeline</typeparam>
/// <typeparam name="TOut">Output type of the current step</typeparam>
public class PipelineStepBuilder<TIn, TOut>
{
    private readonly IPipelineContext _context;
    private readonly ISourceBlock<TIn> _pipelineSource;
    private ISourceBlock<PipelineResult<TOut>> _currentBlock;
    private readonly string _currentStepName;

    internal PipelineStepBuilder(
        IPipelineContext context,
        ISourceBlock<TIn> pipelineSource,
        ISourceBlock<PipelineResult<TOut>> currentBlock,
        string currentStepName)
    {
        _context = context;
        _pipelineSource = pipelineSource;
        _currentBlock = currentBlock;
        _currentStepName = currentStepName;
    }

    /// <summary>
    /// Configure artifact capture for the current step's output
    /// This creates an artifact capture block and updates the current block
    /// </summary>
    public PipelineStepBuilder<TIn, TOut> WithArtifact(
        string? artifactName = null,
        Func<TOut, string>? artifactNameFactory = null,
        Func<TOut, string>? getResourceId = null,
        Func<TOut, object>? getArtifactData = null,
        ArtifactStorageType storageType = ArtifactStorageType.S3)
    {
        if (_context.ArtifactBatcher == null)
        {
            // No artifact batcher configured, skip artifact capture
            return this;
        }

        // Create broadcast to split flow into main pipeline and parallel artifact pipeline
        // Use Unbounded capacity to accept all messages (targets will buffer as needed)
        var broadcastBlock = new BroadcastBlock<PipelineResult<TOut>>(
            result =>
            {
                return result;
            },
            new DataflowBlockOptions 
            { 
                BoundedCapacity = DataflowBlockOptions.Unbounded,
                CancellationToken = _context.CancellationToken 
            });

        // Create large buffer to ensure no message loss from BroadcastBlock
        // This buffer absorbs messages when main pipeline has backpressure
        var artifactBuffer = new BufferBlock<PipelineResult<TOut>>(
            new DataflowBlockOptions 
            { 
                BoundedCapacity = DataflowBlockOptions.Unbounded,
                CancellationToken = _context.CancellationToken 
            });

        // Create parallel artifact saving block (fire-and-forget side effect)
        var artifactBlock = new ActionBlock<PipelineResult<TOut>>(async result =>
        {
            // Only save artifacts for successful results
            if (!result.IsSuccess || result.Value == null)
            {
                return;
            }

            try
            {
                var name = artifactNameFactory != null
                    ? artifactNameFactory(result.Value)
                    : artifactName ?? $"{_currentStepName}_output";

                var data = getArtifactData != null
                    ? getArtifactData(result.Value)
                    : result.Value;

                var resourceId = getResourceId != null
                    ? getResourceId(result.Value)
                    : result.ResourceId;

                Guid resourceRunId;
                if (_context.ResourceRunCache != null)
                {
                    resourceRunId = await _context.ResourceRunCache.GetOrCreateAsync(
                        _context.RunId,
                        resourceId,
                        _context.ResourceTypeName,
                        _context.CancellationToken);
                }
                else
                {
                    resourceRunId = Guid.NewGuid();
                }

                await _context.ArtifactBatcher.QueueArtifactSaveAsync(
                    new ThirdOpinion.DataFlow.Artifacts.Models.ArtifactSaveRequest
                    {
                        ResourceRunId = resourceRunId,
                        StepName = _currentStepName,
                        ArtifactName = name,
                        Data = data,
                        StorageTypeOverride = storageType
                    },
                    _context.CancellationToken);
            }
            catch (Exception ex)
            {
                _context.Logger.LogError(ex, "Error queuing artifact for step {StepName}", _currentStepName);
            }
        }, new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = DataflowBlockOptions.Unbounded,
            CancellationToken = _context.CancellationToken
        });

        // CRITICAL ORDER: Set up all targets BEFORE linking source to broadcast
        // This ensures BroadcastBlock has targets ready before receiving messages
        
        // Step 1: Link broadcast → artifact buffer (MUST propagate completion for BroadcastBlock to work correctly!)
        broadcastBlock.LinkTo(artifactBuffer, new DataflowLinkOptions { PropagateCompletion = true });
        
        // Step 2: Link buffer → artifact action block (propagate completion so we can track when it finishes)
        artifactBuffer.LinkTo(artifactBlock, new DataflowLinkOptions { PropagateCompletion = true });

        // Step 3: NOW link current block → broadcast (starts data flow)
        _currentBlock.LinkTo(broadcastBlock, new DataflowLinkOptions { PropagateCompletion = true });

        // CRITICAL FIX: Insert unbounded buffer AFTER broadcast to prevent backpressure
        // BroadcastBlock requires ALL targets to be ready. If downstream has bounded capacity,
        // it will apply backpressure and BroadcastBlock will DROP messages!
        // Solution: Buffer between broadcast and next step absorbs backpressure.
        var mainPipelineBuffer = new BufferBlock<PipelineResult<TOut>>(
            new DataflowBlockOptions 
            { 
                BoundedCapacity = DataflowBlockOptions.Unbounded,
                CancellationToken = _context.CancellationToken 
            });
        
        // Link broadcast → main pipeline buffer (propagate completion)
        broadcastBlock.LinkTo(mainPipelineBuffer, new DataflowLinkOptions { PropagateCompletion = true });

        // Update current block to the main pipeline buffer
        // The next Transform/Batch/ExecuteAsync will link FROM this buffer
        _currentBlock = mainPipelineBuffer;

        if (_context is PipelineContext pipelineContext)
        {
            pipelineContext.RegisterArtifactBlock(artifactBlock);
        }

        return this;
    }

    /// <summary>
    /// Add another transformation step (synchronous)
    /// </summary>
    public PipelineStepBuilder<TIn, TNext> Transform<TNext>(
        Func<TOut, TNext> transform,
        string stepName,
        PipelineStepOptions? options = null)
    {
        return Transform(data => Task.FromResult(transform(data)), stepName, options);
    }

    /// <summary>
    /// Add another transformation step (asynchronous)
    /// </summary>
    public PipelineStepBuilder<TIn, TNext> Transform<TNext>(
        Func<TOut, Task<TNext>> transformAsync,
        string stepName,
        PipelineStepOptions? options = null)
    {
        var execOptions = CreateExecutionOptions(options);
        
        var transformBlock = TrackedBlockFactory.CreateDownstreamTrackedBlock(
            transformAsync,
            stepName,
            _context,
            execOptions);

        _currentBlock.LinkTo(transformBlock, new DataflowLinkOptions { PropagateCompletion = true });

        return new PipelineStepBuilder<TIn, TNext>(
            _context,
            _pipelineSource,
            transformBlock,
            stepName);
    }

    /// <summary>
    /// Group ordered outputs from the current step into sequential batches
    /// </summary>
    public PipelineStepBuilder<TIn, TGroup> GroupSequential<TGroup, TKey>(
        Func<TOut, TKey> keySelector,
        Func<TKey, IReadOnlyList<TOut>, TGroup> projector,
        Func<TKey, string> getResourceIdFromKey,
        string stepName,
        PipelineStepOptions? options = null)
        where TKey : notnull
    {
        var execOptions = CreateExecutionOptions(options);

        var groupingBlock = TrackedBlockFactory.CreateSequentialGroupingBlock(
            keySelector,
            projector,
            getResourceIdFromKey,
            stepName,
            _context,
            execOptions);

        _currentBlock.LinkTo(groupingBlock, new DataflowLinkOptions { PropagateCompletion = true });

        return new PipelineStepBuilder<TIn, TGroup>(
            _context,
            _pipelineSource,
            groupingBlock,
            stepName);
    }

    /// <summary>
    /// Add a TransformMany step that expands one input to multiple outputs
    /// </summary>
    public PipelineStepBuilder<TIn, TNext> TransformMany<TNext>(
        Func<TOut, Task<IEnumerable<TNext>>> transformManyAsync,
        Func<TNext, string> getResourceIdFromOutput,
        string stepName,
        PipelineStepOptions? options = null)
    {
        var execOptions = CreateExecutionOptions(options);

        var transformManyBlock = TrackedBlockFactory.CreateDownstreamTrackedTransformMany(
            transformManyAsync,
            getResourceIdFromOutput,
            stepName,
            _context,
            execOptions);

        _currentBlock.LinkTo(transformManyBlock, new DataflowLinkOptions { PropagateCompletion = true });

        return new PipelineStepBuilder<TIn, TNext>(
            _context,
            _pipelineSource,
            transformManyBlock,
            stepName);
    }

    /// <summary>
    /// Create batches of items - transforms from T to T[]
    /// </summary>
    public PipelineStepBuilder<TIn, TOut[]> Batch(int batchSize, PipelineStepOptions? options = null)
    {
        var groupingOptions = new GroupingDataflowBlockOptions
        {
            CancellationToken = _context.CancellationToken
        };
        
        if (options != null)
        {
            groupingOptions.BoundedCapacity = options.BoundedCapacity;
            // Note: BatchBlock doesn't support MaxDegreeOfParallelism
        }
        
        var batchBlock = new BatchBlock<PipelineResult<TOut>>(batchSize, groupingOptions);

        _currentBlock.LinkTo(batchBlock, new DataflowLinkOptions { PropagateCompletion = true });

        // Transform the batch block output to match PipelineResult<TOut[]> format
        var transformBlock = new TransformBlock<PipelineResult<TOut>[], PipelineResult<TOut[]>>(
            batch =>
            {
                // Check if any items in the batch failed
                var failedItems = batch.Where(r => !r.IsSuccess).ToArray();
                if (failedItems.Any())
                {
                    // Log failures but continue with successful items
                    foreach (var failed in failedItems)
                    {
                        _context.Logger.LogWarning(
                            "Batch contains failed resource {ResourceId} - Error: {ErrorMessage}",
                            failed.ResourceId,
                            failed.ErrorMessage);
                    }
                }

                // Get successful items
                var successfulItems = batch.Where(r => r.IsSuccess && r.Value != null).ToArray();
                
                // Create a batch result using the first resource ID (or empty string if no successful items)
                var resourceId = successfulItems.Length > 0 ? successfulItems[0].ResourceId : "";
                var values = successfulItems.Select(r => r.Value!).ToArray();
                
                return PipelineResult<TOut[]>.Success(values, resourceId);
            },
            new ExecutionDataflowBlockOptions
            {
                CancellationToken = _context.CancellationToken
            });

        batchBlock.LinkTo(transformBlock, new DataflowLinkOptions { PropagateCompletion = true });

        return new PipelineStepBuilder<TIn, TOut[]>(
            _context,
            _pipelineSource,
            transformBlock,
            "Batch");
    }

    /// <summary>
    /// Execute an action for each item in the pipeline (pass-through operation with tracking)
    /// </summary>
    public PipelineStepBuilder<TIn, TOut> Action(
        Func<TOut, Task> action, 
        string stepName,
        PipelineStepOptions? options = null)
    {
        var execOptions = CreateExecutionOptions(options);
        
        // Create a transform block that performs the action and passes data through
        var actionTransformBlock = TrackedBlockFactory.CreateDownstreamTrackedBlock(
            async (TOut data) =>
            {
                await action(data);
                return data; // Pass through unchanged
            },
            stepName,
            _context,
            execOptions);

        _currentBlock.LinkTo(actionTransformBlock, new DataflowLinkOptions { PropagateCompletion = true });

        return new PipelineStepBuilder<TIn, TOut>(
            _context,
            _pipelineSource,
            actionTransformBlock,
            stepName);
    }

    /// <summary>
    /// Complete the pipeline and wait for all processing to finish (terminal operation)
    /// </summary>
    /// <param name="getResourceId">Function to extract a single resource ID from the final output</param>
    public Task Complete(Func<TOut, string> getResourceId)
        => Complete(output => new[] { getResourceId(output) });

    /// <summary>
    /// Complete the pipeline and wait for all processing to finish (terminal operation)
    /// </summary>
    /// <param name="getResourceIds">Function to extract multiple resource IDs from the final output (e.g., for batches)</param>
    public async Task Complete(Func<TOut, IEnumerable<string>>? getResourceIds = null)
    {
        // Create a final consuming block that drains the pipeline and marks resources as complete
        var finalBlock = new ActionBlock<PipelineResult<TOut>>(result =>
        {
            if (result.IsSuccess && result.Value != null)
            {
                // Extract resource IDs to mark as complete
                if (getResourceIds != null)
                {
                    try
                    {
                        var resourceIds = getResourceIds(result.Value);
                        foreach (var resourceId in resourceIds)
                        {
                            _context.ProgressTracker?.RecordResourceComplete(resourceId, PipelineResourceStatus.Completed);
                        }
                    }
                    catch (Exception ex)
                    {
                        _context.Logger.LogError(ex, "Error extracting resource IDs for completion tracking");
                    }
                }
                else
                {
                    // No extractor provided - mark the result's resource ID as complete
                    _context.ProgressTracker?.RecordResourceComplete(result.ResourceId, PipelineResourceStatus.Completed);
                }
            }
            else if (!result.IsSuccess)
            {
                _context.Logger.LogWarning("Pipeline completed with failed resource {ResourceId}: {ErrorMessage}", 
                    result.ResourceId, result.ErrorMessage);
            }
            return Task.CompletedTask;
        }, new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = DataflowBlockOptions.Unbounded,
            CancellationToken = _context.CancellationToken
        });

        _currentBlock.LinkTo(finalBlock, new DataflowLinkOptions { PropagateCompletion = true });

        await finalBlock.Completion;

        if (_context is PipelineContext pipelineContextForArtifacts)
        {
            await pipelineContextForArtifacts.WaitForArtifactBlocksAsync();
        }

        // Finalize tracking and artifacts
        if (_context.ProgressTracker != null)
        {
            await _context.ProgressTracker.FinalizeAsync();
        }

        if (_context.ArtifactBatcher != null)
        {
            await _context.ArtifactBatcher.FinalizeAsync();
            if (_context is PipelineContext pipelineContextForFinalize)
            {
                pipelineContextForFinalize.MarkArtifactBatcherFinalized();
            }
        }

        // Dispose context (will dispose progress tracker if it implements IAsyncDisposable/IDisposable)
        if (_context is IAsyncDisposable asyncDisposableContext)
        {
            await asyncDisposableContext.DisposeAsync();
        }
    }

    /// <summary>
    /// Build the pipeline as a propagator block (for advanced scenarios)
    /// </summary>
    public IPropagatorBlock<TIn, PipelineResult<TOut>> Build()
    {
        // This is a simplified version - in a real implementation,
        // you'd return a proper propagator block that encapsulates the entire chain
        throw new NotImplementedException("Build() is reserved for advanced scenarios");
    }

    private ExecutionDataflowBlockOptions CreateExecutionOptions(PipelineStepOptions? options)
    {
        var stepOpts = options ?? _context.DefaultStepOptions;
        return new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = stepOpts.MaxDegreeOfParallelism,
            BoundedCapacity = stepOpts.BoundedCapacity,
            CancellationToken = _context.CancellationToken
        };
    }
}

