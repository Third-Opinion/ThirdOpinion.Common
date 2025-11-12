using System.Threading.Tasks.Dataflow;
using ThirdOpinion.Common.DataFlow.Blocks;
using ThirdOpinion.Common.DataFlow.Results;

namespace ThirdOpinion.Common.DataFlow.Core;

/// <summary>
/// Fluent pipeline builder for creating data processing pipelines
/// </summary>
/// <typeparam name="TData">Type of data being processed</typeparam>
public class DataFlowPipeline<TData>
{
    private readonly IPipelineContext _context;
    private readonly Func<TData, string> _resourceIdSelector;
    private ISourceBlock<TData>? _sourceBlock;

    private DataFlowPipeline(IPipelineContext context, Func<TData, string> resourceIdSelector)
    {
        _context = context;
        _resourceIdSelector = resourceIdSelector;
    }

    /// <summary>
    /// Create a new pipeline
    /// </summary>
    public static DataFlowPipeline<TData> Create(
        IPipelineContext context,
        Func<TData, string> resourceIdSelector)
    {
        return new DataFlowPipeline<TData>(context, resourceIdSelector);
    }

    /// <summary>
    /// Set the source from an async enumerable
    /// </summary>
    public DataFlowPipeline<TData> FromAsyncSource(IAsyncEnumerable<TData> source)
    {
        _sourceBlock = DataFlowBlockFactory.CreateAsyncEnumerableSource(source, _context.CancellationToken);
        return this;
    }

    /// <summary>
    /// Set the source from a source block
    /// </summary>
    public DataFlowPipeline<TData> FromSource(ISourceBlock<TData> sourceBlock)
    {
        _sourceBlock = sourceBlock;
        return this;
    }

    /// <summary>
    /// Set the source from a synchronous enumerable
    /// </summary>
    public DataFlowPipeline<TData> FromEnumerable(IEnumerable<TData> source)
    {
        _sourceBlock = DataFlowBlockFactory.CreateEnumerableSource(source, _context.CancellationToken);
        return this;
    }

    /// <summary>
    /// Add a transformation step (synchronous)
    /// </summary>
    public PipelineStepBuilder<TData, TOutput> Transform<TOutput>(
        Func<TData, TOutput> transform,
        string stepName,
        PipelineStepOptions? options = null)
    {
        return Transform(data => Task.FromResult(transform(data)), stepName, options);
    }

    /// <summary>
    /// Add a transformation step (asynchronous)
    /// </summary>
    public PipelineStepBuilder<TData, TOutput> Transform<TOutput>(
        Func<TData, Task<TOutput>> transformAsync,
        string stepName,
        PipelineStepOptions? options = null)
    {
        if (_sourceBlock == null)
            throw new InvalidOperationException("Source must be set before adding transformation steps");

        var execOptions = CreateExecutionOptions(options);
        var transformBlock = TrackedBlockFactory.CreateInitialTrackedBlock(
            transformAsync,
            _resourceIdSelector,
            stepName,
            _context,
            execOptions);

        _sourceBlock.LinkTo(transformBlock, new DataflowLinkOptions { PropagateCompletion = true });

        return new PipelineStepBuilder<TData, TOutput>(
            _context,
            _sourceBlock,
            transformBlock,
            stepName);
    }

    /// <summary>
    /// Group ordered inputs into sequential batches using the provided key selector
    /// </summary>
    public PipelineStepBuilder<TData, TGroup> GroupSequential<TGroup, TKey>(
        Func<TData, TKey> keySelector,
        Func<TKey, IReadOnlyList<TData>, TGroup> projector,
        Func<TKey, string> getResourceIdFromKey,
        string stepName,
        PipelineStepOptions? options = null)
        where TKey : notnull
    {
        if (_sourceBlock == null)
            throw new InvalidOperationException("Source must be set before adding transformation steps");

        var execOptions = CreateExecutionOptions(options);

        var initialBlock = TrackedBlockFactory.CreateInitialTrackedBlock<TData, TData>(
            data => Task.FromResult(data),
            _resourceIdSelector,
            $"{stepName}_Init",
            _context,
            execOptions);

        var groupingBlock = TrackedBlockFactory.CreateSequentialGroupingBlock<TData, TGroup, TKey>(
            keySelector,
            projector,
            getResourceIdFromKey,
            stepName,
            _context,
            execOptions);

        _sourceBlock.LinkTo(initialBlock, new DataflowLinkOptions { PropagateCompletion = true });
        initialBlock.LinkTo(groupingBlock, new DataflowLinkOptions { PropagateCompletion = true });

        return new PipelineStepBuilder<TData, TGroup>(
            _context,
            _sourceBlock,
            groupingBlock,
            stepName);
    }

    /// <summary>
    /// Add a TransformMany step that expands one input to multiple outputs
    /// </summary>
    public PipelineStepBuilder<TData, TOutput> TransformMany<TOutput>(
        Func<TData, Task<IEnumerable<TOutput>>> transformManyAsync,
        Func<TOutput, string> getResourceIdFromOutput,
        string stepName,
        PipelineStepOptions? options = null)
    {
        if (_sourceBlock == null)
            throw new InvalidOperationException("Source must be set before adding transformation steps");

        var execOptions = CreateExecutionOptions(options);

        // Create initial transform to PipelineResult
        var initialBlock = TrackedBlockFactory.CreateInitialTrackedBlock<TData, TData>(
            data => Task.FromResult(data), // Pass-through
            _resourceIdSelector,
            $"{stepName}_Init",
            _context,
            execOptions);

        // Create TransformMany block
        var transformManyBlock = TrackedBlockFactory.CreateDownstreamTrackedTransformMany(
            transformManyAsync,
            getResourceIdFromOutput,
            stepName,
            _context,
            execOptions);

        _sourceBlock.LinkTo(initialBlock, new DataflowLinkOptions { PropagateCompletion = true });
        initialBlock.LinkTo(transformManyBlock, new DataflowLinkOptions { PropagateCompletion = true });

        return new PipelineStepBuilder<TData, TOutput>(
            _context,
            _sourceBlock,
            transformManyBlock,
            stepName);
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

