using System.Threading.Tasks.Dataflow;

namespace ThirdOpinion.Common.DataFlow.Extensions;

/// <summary>
/// Extension methods for dataflow blocks
/// </summary>
public static class DataflowBlockExtensions
{
    /// <summary>
    /// Link source to target and propagate completion
    /// </summary>
    public static IDisposable LinkToWithCompletion<T>(
        this ISourceBlock<T> source,
        ITargetBlock<T> target)
    {
        return source.LinkTo(target, new DataflowLinkOptions { PropagateCompletion = true });
    }

    /// <summary>
    /// Link source to target with a predicate and propagate completion
    /// </summary>
    public static IDisposable LinkToWithCompletion<T>(
        this ISourceBlock<T> source,
        ITargetBlock<T> target,
        Predicate<T> predicate)
    {
        return source.LinkTo(target, new DataflowLinkOptions { PropagateCompletion = true }, predicate);
    }

    /// <summary>
    /// Wait for completion and propagate exceptions
    /// </summary>
    public static async Task WaitForCompletionAsync(
        this IDataflowBlock block,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await block.Completion.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected cancellation
            throw;
        }
        catch (Exception)
        {
            // Propagate any exceptions from the block
            throw;
        }
    }

    /// <summary>
    /// Complete the block and wait for it to finish
    /// </summary>
    public static async Task CompleteAndWaitAsync(
        this IDataflowBlock block,
        CancellationToken cancellationToken = default)
    {
        block.Complete();
        await block.WaitForCompletionAsync(cancellationToken);
    }

    /// <summary>
    /// Post multiple items to a target block
    /// </summary>
    public static void PostMany<T>(
        this ITargetBlock<T> target,
        IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            target.Post(item);
        }
    }

    /// <summary>
    /// Send multiple items to a target block asynchronously
    /// </summary>
    public static async Task SendManyAsync<T>(
        this ITargetBlock<T> target,
        IEnumerable<T> items,
        CancellationToken cancellationToken = default)
    {
        foreach (var item in items)
        {
            await target.SendAsync(item, cancellationToken);
        }
    }

    /// <summary>
    /// Create a cancellable completion task
    /// </summary>
    public static Task WaitAsync(
        this Task task,
        CancellationToken cancellationToken)
    {
        if (!cancellationToken.CanBeCanceled)
            return task;

        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled(cancellationToken);

        return task.WaitAsyncCore(cancellationToken);
    }

    private static async Task WaitAsyncCore(this Task task, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<bool>();
        using (cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken)))
        {
            var completedTask = await Task.WhenAny(task, tcs.Task);
            if (completedTask == tcs.Task)
            {
                // Cancellation won
                throw new OperationCanceledException(cancellationToken);
            }
            
            // Original task won - propagate its result/exception
            await task;
        }
    }
}

