using System.Threading.Tasks.Dataflow;
using ThirdOpinion.Common.IA.Pipelines.Blocks;
using ThirdOpinion.Common.IA.Pipelines.Models;
using ThirdOpinion.Common.IA.Pipelines.Progress;

namespace ThirdOpinion.Common.IA.Pipelines.Core;

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
    /// </summary>
    /// <param name="progressService">Progress service used to query incomplete resources.</param>
    /// <param name="freshSourceFactory">Factory for fresh runs.</param>
    /// <param name="loadIncompleteAsync">Factory invoked for retry/continuation runs. Receives incomplete resource IDs.</param>
    public static PipelineSource<T> FromRunType(
        IPipelineProgressService progressService,
        Func<IEnumerable<T>> freshSourceFactory,
        Func<IEnumerable<string>, CancellationToken, IAsyncEnumerable<T>> loadIncompleteAsync)
    {
        if (progressService == null)
            throw new ArgumentNullException(nameof(progressService));
        if (freshSourceFactory == null)
            throw new ArgumentNullException(nameof(freshSourceFactory));
        if (loadIncompleteAsync == null)
            throw new ArgumentNullException(nameof(loadIncompleteAsync));

        return new PipelineSource<T>((context, ct) =>
        {
            var runType = context.RunType;

            if (runType == PipelineRunType.Fresh)
            {
                var freshData = freshSourceFactory();
                if (freshData == null)
                    throw new InvalidOperationException("Fresh source factory returned null enumerable.");

                return DataFlowBlockFactory.CreateEnumerableSource(freshData, ct);
            }

            var referenceRunId = context.ParentRunId ?? context.RunId;

            var incompleteIds = progressService.GetIncompleteResourceIdsAsync(referenceRunId, ct)
                .GetAwaiter()
                .GetResult();

            var retryData = loadIncompleteAsync(incompleteIds, ct);
            if (retryData == null)
                throw new InvalidOperationException("Retry loader returned null async enumerable.");

            return DataFlowBlockFactory.CreateAsyncEnumerableSource(retryData, ct);
        });
    }
}

