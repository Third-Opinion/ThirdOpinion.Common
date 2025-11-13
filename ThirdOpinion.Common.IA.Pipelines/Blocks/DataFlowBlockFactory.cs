using System.Threading.Tasks.Dataflow;

namespace ThirdOpinion.Common.IA.Pipelines.Blocks;

/// <summary>
/// Factory for creating common dataflow block patterns
/// </summary>
public static class DataFlowBlockFactory
{
    /// <summary>
    /// Create a source block from an async enumerable
    /// </summary>
    public static BufferBlock<T> CreateAsyncEnumerableSource<T>(
        IAsyncEnumerable<T> source,
        CancellationToken ct = default)
    {
        var buffer = new BufferBlock<T>();

        Task.Run(async () =>
        {
            try
            {
                await foreach (var item in source.WithCancellation(ct))
                {
                    await buffer.SendAsync(item, ct);
                }
            }
            catch (Exception)
            {
                // Log or handle exception
                throw;
            }
            finally
            {
                buffer.Complete();
            }
        }, ct);

        return buffer;
    }

    /// <summary>
    /// Create a source block from a synchronous enumerable
    /// </summary>
    public static BufferBlock<T> CreateEnumerableSource<T>(
        IEnumerable<T> source,
        CancellationToken ct = default)
    {
        var buffer = new BufferBlock<T>();

        Task.Run(() =>
        {
            try
            {
                foreach (var item in source)
                {
                    ct.ThrowIfCancellationRequested();
                    buffer.Post(item);
                }
            }
            catch (Exception)
            {
                // Log or handle exception
                throw;
            }
            finally
            {
                buffer.Complete();
            }
        }, ct);

        return buffer;
    }

    /// <summary>
    /// Create a grouping block that groups items by key
    /// </summary>
    public static TransformManyBlock<TInput, IGrouping<TKey, TInput>> CreateGroupingBlock<TInput, TKey>(
        Func<TInput, TKey> keySelector,
        ExecutionDataflowBlockOptions? options = null)
        where TKey : notnull
    {
        var groups = new Dictionary<TKey, List<TInput>>();
        var opts = options ?? new ExecutionDataflowBlockOptions();

        return new TransformManyBlock<TInput, IGrouping<TKey, TInput>>(
            item =>
            {
                var key = keySelector(item);
                if (!groups.ContainsKey(key))
                {
                    groups[key] = new List<TInput>();
                }
                groups[key].Add(item);
                return Array.Empty<IGrouping<TKey, TInput>>();
            },
            opts);
    }

    /// <summary>
    /// Create an aggregation block that collects all items
    /// </summary>
    public static TransformBlock<TInput, List<TInput>> CreateAggregationBlock<TInput>(
        ExecutionDataflowBlockOptions? options = null)
    {
        var items = new List<TInput>();
        var opts = options ?? new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 1 };

        return new TransformBlock<TInput, List<TInput>>(
            item =>
            {
                items.Add(item);
                return items;
            },
            opts);
    }

    /// <summary>
    /// Create a batch block with specified batch size
    /// </summary>
    public static BatchBlock<T> CreateBatchBlock<T>(
        int batchSize,
        GroupingDataflowBlockOptions? options = null)
    {
        return new BatchBlock<T>(batchSize, options ?? new GroupingDataflowBlockOptions());
    }

    /// <summary>
    /// Create a broadcast block that sends items to multiple targets
    /// </summary>
    public static BroadcastBlock<T> CreateBroadcastBlock<T>(
        Func<T, T>? cloningFunction = null,
        DataflowBlockOptions? options = null)
    {
        return new BroadcastBlock<T>(cloningFunction, options ?? new DataflowBlockOptions());
    }

    /// <summary>
    /// Link source to target with a fallback target for filtered items
    /// </summary>
    public static IDisposable LinkWithFallback<T>(
        ISourceBlock<T> source,
        ITargetBlock<T> target,
        ITargetBlock<T> fallback,
        Predicate<T> predicate)
    {
        var link1 = source.LinkTo(target, new DataflowLinkOptions { PropagateCompletion = true }, predicate);
        var link2 = source.LinkTo(fallback, new DataflowLinkOptions { PropagateCompletion = true });
        
        return new CompositeDisposable(link1, link2);
    }

    private class CompositeDisposable : IDisposable
    {
        private readonly IDisposable[] _disposables;

        public CompositeDisposable(params IDisposable[] disposables)
        {
            _disposables = disposables;
        }

        public void Dispose()
        {
            foreach (var disposable in _disposables)
            {
                disposable.Dispose();
            }
        }
    }
}

