using System.Threading.Tasks.Dataflow;
using ThirdOpinion.Common.DataFlow.Blocks;

namespace ThirdOpinion.Common.DataFlow.Core;

/// <summary>
/// Fluent pipeline builder for creating data processing pipelines
/// </summary>
/// <typeparam name="TData">Type of data being processed</typeparam>
public class DataFlowPipeline<TData>
{
    private readonly IPipelineContext _context;
    private readonly Func<TData, string> _resourceIdSelector;
    private PipelineSource<TData>? _sourceDefinition;
    private ISourceBlock<TData>? _sourceBlock;

    private DataFlowPipeline(IPipelineContext context, Func<TData, string> resourceIdSelector)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _resourceIdSelector = resourceIdSelector ?? throw new ArgumentNullException(nameof(resourceIdSelector));
    }

    private DataFlowPipeline(IPipelineContext context, PipelineSource<TData> source, Func<TData, string> resourceIdSelector)
        : this(context, resourceIdSelector)
    {
        _sourceDefinition = source ?? throw new ArgumentNullException(nameof(source));
    }

    /// <summary>
    /// Create a new pipeline without assigning a source yet.
    /// Call <see cref="WithSource"/> (or an equivalent helper) before adding steps.
    /// </summary>
    public static DataFlowPipeline<TData> Create(
        IPipelineContext context,
        Func<TData, string> resourceIdSelector)
    {
        return new DataFlowPipeline<TData>(context, resourceIdSelector);
    }

    /// <summary>
    /// Create a new pipeline with the provided source definition.
    /// </summary>
    public static DataFlowPipeline<TData> Create(
        IPipelineContext context,
        PipelineSource<TData> source,
        Func<TData, string> resourceIdSelector)
    {
        return new DataFlowPipeline<TData>(context, source, resourceIdSelector);
    }

    /// <summary>
    /// Assign the data source for this pipeline.
    /// </summary>
    public DataFlowPipeline<TData> WithSource(PipelineSource<TData> source)
    {
        _sourceDefinition = source ?? throw new ArgumentNullException(nameof(source));
        _sourceBlock = null;
        return this;
    }

    /// <summary>
    /// Set the source from a synchronous enumerable.
    /// </summary>
    public DataFlowPipeline<TData> FromEnumerable(IEnumerable<TData> source)
    {
        return WithSource(PipelineSource<TData>.FromEnumerable(source));
    }

    /// <summary>
    /// Set the source from a synchronous enumerable factory.
    /// </summary>
    public DataFlowPipeline<TData> FromEnumerable(Func<IEnumerable<TData>> sourceFactory)
    {
        return WithSource(PipelineSource<TData>.FromEnumerable(sourceFactory));
    }

    /// <summary>
    /// Set the source from an async enumerable.
    /// </summary>
    public DataFlowPipeline<TData> FromAsyncSource(IAsyncEnumerable<TData> source)
    {
        return WithSource(PipelineSource<TData>.FromAsyncEnumerable(source));
    }

    /// <summary>
    /// Set the source from an async enumerable factory.
    /// </summary>
    public DataFlowPipeline<TData> FromAsyncSource(Func<CancellationToken, IAsyncEnumerable<TData>> sourceFactory)
    {
        return WithSource(PipelineSource<TData>.FromAsyncEnumerable(sourceFactory));
    }

    /// <summary>
    /// Set the source from an async enumerable factory that considers pipeline context.
    /// </summary>
    public DataFlowPipeline<TData> FromContextualAsyncSource(Func<IPipelineContext, CancellationToken, IAsyncEnumerable<TData>> sourceFactory)
    {
        return WithSource(PipelineSource<TData>.FromAsyncEnumerable(sourceFactory));
    }

    /// <summary>
    /// Set the source directly from an existing dataflow block factory.
    /// </summary>
    public DataFlowPipeline<TData> FromSource(Func<IPipelineContext, CancellationToken, ISourceBlock<TData>> sourceFactory)
    {
        return WithSource(PipelineSource<TData>.FromBlock(sourceFactory));
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
        var sourceBlock = EnsureSourceBlock();

        var execOptions = CreateExecutionOptions(options);
        var transformBlock = TrackedBlockFactory.CreateInitialTrackedBlock(
            transformAsync,
            _resourceIdSelector,
            stepName,
            _context,
            execOptions);

        sourceBlock.LinkTo(transformBlock, new DataflowLinkOptions { PropagateCompletion = true });

        return new PipelineStepBuilder<TData, TOutput>(
            _context,
            sourceBlock,
            transformBlock,
            stepName);
    }

    /// <summary>
    /// Group ordered inputs into sequential batches using the provided key selector.
    /// 
    /// <para><strong>IMPORTANT: This method requires ORDERED input sources only.</strong></para>
    /// 
    /// <para>The method accumulates items with the same key and waits until the key changes 
    /// before emitting a complete group. This means:</para>
    /// <list type="bullet">
    /// <item><description>Input items MUST arrive in order by the grouping key</description></item>
    /// <item><description>The method will wait for a key change before emitting the previous group</description></item>
    /// <item><description>If items with the same key arrive out of order, they will NOT be grouped together correctly</description></item>
    /// <item><description>Processing is single-threaded (MaxDegreeOfParallelism = 1) to maintain order</description></item>
    /// </list>
    /// 
    /// <para>Use this method when you have a pre-sorted source (e.g., from a database query with ORDER BY) 
    /// and need to group consecutive items with the same key into batches.</para>
    /// </summary>
    /// <param name="keySelector">Function to extract the grouping key from each input item</param>
    /// <param name="projector">Function to create the output group from the key and accumulated items</param>
    /// <param name="getResourceIdFromKey">Function to extract the resource ID from the grouping key for progress tracking</param>
    /// <param name="stepName">Name of this step for progress tracking</param>
    /// <param name="options">Optional step configuration options</param>
    /// <typeparam name="TGroup">Type of the grouped output</typeparam>
    /// <typeparam name="TKey">Type of the grouping key (must be non-nullable)</typeparam>
    /// <returns>A builder for the next pipeline step</returns>
    /// <exception cref="InvalidOperationException">Thrown if the input source is not ordered by the grouping key</exception>
    public PipelineStepBuilder<TData, TGroup> GroupSequential<TGroup, TKey>(
        Func<TData, TKey> keySelector,
        Func<TKey, IReadOnlyList<TData>, TGroup> projector,
        Func<TKey, string> getResourceIdFromKey,
        string stepName,
        PipelineStepOptions? options = null)
        where TKey : notnull
    {
        var sourceBlock = EnsureSourceBlock();

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

        sourceBlock.LinkTo(initialBlock, new DataflowLinkOptions { PropagateCompletion = true });
        initialBlock.LinkTo(groupingBlock, new DataflowLinkOptions { PropagateCompletion = true });

        return new PipelineStepBuilder<TData, TGroup>(
            _context,
            sourceBlock,
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
        var sourceBlock = EnsureSourceBlock();

        var execOptions = CreateExecutionOptions(options);

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

        sourceBlock.LinkTo(initialBlock, new DataflowLinkOptions { PropagateCompletion = true });
        initialBlock.LinkTo(transformManyBlock, new DataflowLinkOptions { PropagateCompletion = true });

        return new PipelineStepBuilder<TData, TOutput>(
            _context,
            sourceBlock,
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

    private ISourceBlock<TData> EnsureSourceBlock()
    {
        if (_sourceBlock != null)
        {
            return _sourceBlock;
        }

        if (_sourceDefinition == null)
        {
            throw new InvalidOperationException("Pipeline source has not been configured. Call WithSource (or an equivalent helper) before adding steps.");
        }

        _sourceBlock = _sourceDefinition.Create(_context);
        return _sourceBlock;
    }
}

