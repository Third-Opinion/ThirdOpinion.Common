using System.Threading.Tasks.Dataflow;
using ThirdOpinion.Common.DataFlow.Blocks;
using ThirdOpinion.Common.DataFlow.Models;
using ThirdOpinion.Common.DataFlow.Progress;

namespace ThirdOpinion.Common.DataFlow.Core;

/// <summary>
/// Defines the data source for a pipeline run.
/// </summary>
/// <typeparam name="T">Data type produced by the source.</typeparam>
public sealed class PipelineSource<T>
{
    private readonly Func<IPipelineContext, CancellationToken, ISourceBlock<T>> _builder;

    private PipelineSource(Func<IPipelineContext, CancellationToken, ISourceBlock<T>> builder)
    {
        _builder = builder ?? throw new ArgumentNullException(nameof(builder));
    }

    internal ISourceBlock<T> Create(IPipelineContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        return _builder(context, context.CancellationToken);
    }

    /// <summary>
    /// Source backed by a synchronous enumerable.
    /// </summary>
    public static PipelineSource<T> FromEnumerable(IEnumerable<T> source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        return new PipelineSource<T>((_, ct) => DataFlowBlockFactory.CreateEnumerableSource(source, ct));
    }

    /// <summary>
    /// Source backed by a synchronous enumerable factory.
    /// </summary>
    public static PipelineSource<T> FromEnumerable(Func<IEnumerable<T>> sourceFactory)
    {
        if (sourceFactory == null)
            throw new ArgumentNullException(nameof(sourceFactory));

        return new PipelineSource<T>((_, ct) =>
        {
            var data = sourceFactory();
            if (data == null)
                throw new InvalidOperationException("Source factory returned null enumerable.");

            return DataFlowBlockFactory.CreateEnumerableSource(data, ct);
        });
    }

    /// <summary>
    /// Source backed by an async enumerable.
    /// </summary>
    public static PipelineSource<T> FromAsyncEnumerable(IAsyncEnumerable<T> source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        return new PipelineSource<T>((_, ct) => DataFlowBlockFactory.CreateAsyncEnumerableSource(source, ct));
    }

    /// <summary>
    /// Source backed by an async enumerable factory.
    /// </summary>
    public static PipelineSource<T> FromAsyncEnumerable(Func<CancellationToken, IAsyncEnumerable<T>> sourceFactory)
    {
        if (sourceFactory == null)
            throw new ArgumentNullException(nameof(sourceFactory));

        return new PipelineSource<T>((_, ct) =>
        {
            var enumerable = sourceFactory(ct);
            if (enumerable == null)
                throw new InvalidOperationException("Source factory returned null async enumerable.");

            return DataFlowBlockFactory.CreateAsyncEnumerableSource(enumerable, ct);
        });
    }

    /// <summary>
    /// Source backed by an async enumerable factory that can inspect the pipeline context.
    /// </summary>
    public static PipelineSource<T> FromAsyncEnumerable(Func<IPipelineContext, CancellationToken, IAsyncEnumerable<T>> sourceFactory)
    {
        if (sourceFactory == null)
            throw new ArgumentNullException(nameof(sourceFactory));

        return new PipelineSource<T>((context, ct) =>
        {
            var enumerable = sourceFactory(context, ct);
            if (enumerable == null)
                throw new InvalidOperationException("Source factory returned null async enumerable.");

            return DataFlowBlockFactory.CreateAsyncEnumerableSource(enumerable, ct);
        });
    }

    /// <summary>
    /// Source backed by an existing dataflow block factory.
    /// </summary>
    public static PipelineSource<T> FromBlock(Func<IPipelineContext, CancellationToken, ISourceBlock<T>> blockFactory)
    {
        if (blockFactory == null)
            throw new ArgumentNullException(nameof(blockFactory));

        return new PipelineSource<T>(blockFactory);
    }

    /// <summary>
    /// Helper to create a source that automatically selects data based on run type.
    /// The progress service is automatically retrieved from the pipeline context.
    /// </summary>
    /// <param name="freshSourceFactory">Factory for fresh runs.</param>
    /// <param name="loadIncompleteAsync">Factory invoked for retry/continuation runs. Receives incomplete resource IDs.</param>
    public static PipelineSource<T> FromRunType(
        Func<IEnumerable<T>> freshSourceFactory,
        Func<IEnumerable<string>, CancellationToken, IAsyncEnumerable<T>> loadIncompleteAsync)
    {
        if (freshSourceFactory == null)
            throw new ArgumentNullException(nameof(freshSourceFactory));
        if (loadIncompleteAsync == null)
            throw new ArgumentNullException(nameof(loadIncompleteAsync));

        return CreateFromRunType(
            (_, ct) =>
            {
                var data = freshSourceFactory();
                if (data == null)
                    throw new InvalidOperationException("Fresh source factory returned null enumerable.");
                return DataFlowBlockFactory.CreateEnumerableSource(data, ct);
            },
            (incompleteIds, ct) =>
            {
                var data = loadIncompleteAsync(incompleteIds, ct);
                if (data == null)
                    throw new InvalidOperationException("Retry loader returned null async enumerable.");
                return DataFlowBlockFactory.CreateAsyncEnumerableSource(data, ct);
            });
    }

    /// <summary>
    /// Helper to create a source that automatically selects data based on run type.
    /// Overload with async enumerable for fresh source factory.
    /// The progress service is automatically retrieved from the pipeline context.
    /// </summary>
    /// <param name="freshSourceFactory">Factory for fresh runs that returns an async enumerable.</param>
    /// <param name="loadIncompleteAsync">Factory invoked for retry/continuation runs. Receives incomplete resource IDs.</param>
    public static PipelineSource<T> FromRunType(
        Func<CancellationToken, IAsyncEnumerable<T>> freshSourceFactory,
        Func<IEnumerable<string>, CancellationToken, IAsyncEnumerable<T>> loadIncompleteAsync)
    {
        if (freshSourceFactory == null)
            throw new ArgumentNullException(nameof(freshSourceFactory));
        if (loadIncompleteAsync == null)
            throw new ArgumentNullException(nameof(loadIncompleteAsync));

        return CreateFromRunType(
            (_, ct) =>
            {
                var data = freshSourceFactory(ct);
                if (data == null)
                    throw new InvalidOperationException("Fresh source factory returned null async enumerable.");
                return DataFlowBlockFactory.CreateAsyncEnumerableSource(data, ct);
            },
            (incompleteIds, ct) =>
            {
                var data = loadIncompleteAsync(incompleteIds, ct);
                if (data == null)
                    throw new InvalidOperationException("Retry loader returned null async enumerable.");
                return DataFlowBlockFactory.CreateAsyncEnumerableSource(data, ct);
            });
    }

    /// <summary>
    /// Helper to create a source that automatically selects data based on run type.
    /// Overload with synchronous enumerable for incomplete loader.
    /// The progress service is automatically retrieved from the pipeline context.
    /// </summary>
    /// <param name="freshSourceFactory">Factory for fresh runs.</param>
    /// <param name="loadIncomplete">Factory invoked for retry/continuation runs. Receives incomplete resource IDs and returns a synchronous enumerable.</param>
    public static PipelineSource<T> FromRunType(
        Func<IEnumerable<T>> freshSourceFactory,
        Func<IEnumerable<string>, CancellationToken, IEnumerable<T>> loadIncomplete)
    {
        if (freshSourceFactory == null)
            throw new ArgumentNullException(nameof(freshSourceFactory));
        if (loadIncomplete == null)
            throw new ArgumentNullException(nameof(loadIncomplete));

        return CreateFromRunType(
            (_, ct) =>
            {
                var data = freshSourceFactory();
                if (data == null)
                    throw new InvalidOperationException("Fresh source factory returned null enumerable.");
                return DataFlowBlockFactory.CreateEnumerableSource(data, ct);
            },
            (incompleteIds, ct) =>
            {
                var data = loadIncomplete(incompleteIds, ct);
                if (data == null)
                    throw new InvalidOperationException("Retry loader returned null enumerable.");
                return DataFlowBlockFactory.CreateEnumerableSource(data, ct);
            });
    }

    /// <summary>
    /// Helper to create a source that automatically selects data based on run type.
    /// Overload with async enumerable for fresh source factory and synchronous enumerable for incomplete loader.
    /// The progress service is automatically retrieved from the pipeline context.
    /// </summary>
    /// <param name="freshSourceFactory">Factory for fresh runs that returns an async enumerable.</param>
    /// <param name="loadIncomplete">Factory invoked for retry/continuation runs. Receives incomplete resource IDs and returns a synchronous enumerable.</param>
    public static PipelineSource<T> FromRunType(
        Func<CancellationToken, IAsyncEnumerable<T>> freshSourceFactory,
        Func<IEnumerable<string>, CancellationToken, IEnumerable<T>> loadIncomplete)
    {
        if (freshSourceFactory == null)
            throw new ArgumentNullException(nameof(freshSourceFactory));
        if (loadIncomplete == null)
            throw new ArgumentNullException(nameof(loadIncomplete));

        return CreateFromRunType(
            (_, ct) =>
            {
                var data = freshSourceFactory(ct);
                if (data == null)
                    throw new InvalidOperationException("Fresh source factory returned null async enumerable.");
                return DataFlowBlockFactory.CreateAsyncEnumerableSource(data, ct);
            },
            (incompleteIds, ct) =>
            {
                var data = loadIncomplete(incompleteIds, ct);
                if (data == null)
                    throw new InvalidOperationException("Retry loader returned null enumerable.");
                return DataFlowBlockFactory.CreateEnumerableSource(data, ct);
            });
    }

    /// <summary>
    /// Internal helper that implements the common run type selection logic.
    /// </summary>
    /// <param name="createFreshSource">Function that creates the source block for fresh runs.</param>
    /// <param name="createIncompleteSource">Function that creates the source block for incomplete runs, given incomplete resource IDs.</param>
    private static PipelineSource<T> CreateFromRunType(
        Func<IPipelineContext, CancellationToken, ISourceBlock<T>> createFreshSource,
        Func<IEnumerable<string>, CancellationToken, ISourceBlock<T>> createIncompleteSource)
    {
        return new PipelineSource<T>((context, ct) =>
        {
            var runType = context.RunType;

            if (runType == PipelineRunType.Fresh)
            {
                return createFreshSource(context, ct);
            }

            var progressService = context.ProgressService;
            if (progressService == null)
            {
                throw new InvalidOperationException(
                    "IPipelineProgressService is required for retry/continuation runs but was not found in the pipeline context. " +
                    "Ensure the progress service is registered in dependency injection and provided to the PipelineContextFactory.");
            }

            var referenceRunId = context.ParentRunId ?? context.RunId;

            var incompleteIds = progressService.GetIncompleteResourceIdsAsync(referenceRunId, ct)
                .GetAwaiter()
                .GetResult();

            return createIncompleteSource(incompleteIds, ct);
        });
    }
}

