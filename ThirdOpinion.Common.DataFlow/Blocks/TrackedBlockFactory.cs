using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;
using ThirdOpinion.Common.DataFlow.Core;
using ThirdOpinion.Common.DataFlow.Models;
using ThirdOpinion.Common.DataFlow.Results;

namespace ThirdOpinion.Common.DataFlow.Blocks;

/// <summary>
/// Factory for creating tracked dataflow blocks with progress monitoring
/// </summary>
public static class TrackedBlockFactory
{
    /// <summary>
    /// Create the initial tracked block that converts raw input to PipelineResult
    /// </summary>
    public static TransformBlock<TInput, PipelineResult<TOutput>> CreateInitialTrackedBlock<TInput, TOutput>(
        Func<TInput, Task<TOutput>> transformAsync,
        Func<TInput, string> getResourceId,
        string stepName,
        IPipelineContext context,
        ExecutionDataflowBlockOptions? options = null)
    {
        var opts = options ?? new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = 1,
            CancellationToken = context.CancellationToken
        };

        return new TransformBlock<TInput, PipelineResult<TOutput>>(async input =>
        {
            var resourceId = getResourceId(input);
            var sw = Stopwatch.StartNew();

            try
            {
                // Record resource and step start
                context.ProgressTracker?.RecordResourceStart(resourceId, context.ResourceTypeName);
                context.ProgressTracker?.RecordStepStart([resourceId], stepName);

                // Execute transformation
                var output = await transformAsync(input);
                sw.Stop();

                // Record success
                context.ProgressTracker?.RecordStepComplete([resourceId], stepName, (int)sw.ElapsedMilliseconds);

                return PipelineResult<TOutput>.Success(output, resourceId, (int)sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                sw.Stop();
                context.Logger.LogError(ex, "Error in step {StepName} for resource {ResourceId}", stepName, resourceId);
                
                // Record failure
                context.ProgressTracker?.RecordStepFailed([resourceId], stepName, (int)sw.ElapsedMilliseconds, ex.Message);
                context.ProgressTracker?.RecordResourceComplete(resourceId, PipelineResourceStatus.Failed, ex.Message, stepName);

                return PipelineResult<TOutput>.Failure(resourceId, ex.Message, stepName);
            }
        }, opts);
    }

    /// <summary>
    /// Create a downstream tracked block that operates on PipelineResult
    /// </summary>
    public static TransformBlock<PipelineResult<TInput>, PipelineResult<TOutput>> CreateDownstreamTrackedBlock<TInput, TOutput>(
        Func<TInput, Task<TOutput>> transformAsync,
        string stepName,
        IPipelineContext context,
        ExecutionDataflowBlockOptions? options = null)
    {
        var opts = options ?? new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = 1,
            CancellationToken = context.CancellationToken
        };

        return new TransformBlock<PipelineResult<TInput>, PipelineResult<TOutput>>(async result =>
        {
            // Propagate errors from previous steps
            if (!result.IsSuccess)
            {
                return PipelineResult<TOutput>.Failure(
                    result.ResourceId,
                    result.ErrorMessage ?? "Previous step failed",
                    result.ErrorStep);
            }

            var sw = Stopwatch.StartNew();

            try
            {
                // Record step start
                context.ProgressTracker?.RecordStepStart([result.ResourceId], stepName);

                // Execute transformation
                var output = await transformAsync(result.Value!);
                sw.Stop();

                // Record success
                context.ProgressTracker?.RecordStepComplete([result.ResourceId], stepName, (int)sw.ElapsedMilliseconds);

                return PipelineResult<TOutput>.Success(output, result.ResourceId, (int)sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                sw.Stop();
                context.Logger.LogError(ex, "Error in step {StepName} for resource {ResourceId}", stepName, result.ResourceId);
                
                // Record failure
                context.ProgressTracker?.RecordStepFailed([result.ResourceId], stepName, (int)sw.ElapsedMilliseconds, ex.Message);
                context.ProgressTracker?.RecordResourceComplete(result.ResourceId, PipelineResourceStatus.Failed, ex.Message, stepName);

                return PipelineResult<TOutput>.Failure(result.ResourceId, ex.Message, stepName);
            }
        }, opts);
    }

    /// <summary>
    /// Create a downstream TransformMany block that expands one input to many outputs
    /// </summary>
    public static TransformManyBlock<PipelineResult<TInput>, PipelineResult<TOutput>> CreateDownstreamTrackedTransformMany<TInput, TOutput>(
        Func<TInput, Task<IEnumerable<TOutput>>> transformManyAsync,
        Func<TOutput, string> getResourceIdFromOutput,
        string stepName,
        IPipelineContext context,
        ExecutionDataflowBlockOptions? options = null)
    {
        var opts = options ?? new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = 1,
            CancellationToken = context.CancellationToken
        };

        return new TransformManyBlock<PipelineResult<TInput>, PipelineResult<TOutput>>(async result =>
        {
            // Propagate errors from previous steps
            if (!result.IsSuccess)
            {
                return new[] { PipelineResult<TOutput>.Failure(
                    result.ResourceId,
                    result.ErrorMessage ?? "Previous step failed",
                    result.ErrorStep) };
            }

            var sw = Stopwatch.StartNew();

            try
            {
                // Record step start for parent resource
                context.ProgressTracker?.RecordStepStart([result.ResourceId], stepName);

                // Execute transformation
                var outputs = await transformManyAsync(result.Value!);
                sw.Stop();

                // Record success for parent
                context.ProgressTracker?.RecordStepComplete([result.ResourceId], stepName, (int)sw.ElapsedMilliseconds);

                // Convert outputs to PipelineResults and record child resources
                var results = new List<PipelineResult<TOutput>>();
                foreach (var output in outputs)
                {
                    var childResourceId = getResourceIdFromOutput(output);
                    context.ProgressTracker?.RecordResourceStart(childResourceId, context.ResourceTypeName);
                    results.Add(PipelineResult<TOutput>.Success(output, childResourceId));
                }

                return results;
            }
            catch (Exception ex)
            {
                sw.Stop();
                context.Logger.LogError(ex, "Error in step {StepName} for resource {ResourceId}", stepName, result.ResourceId);
                
                // Record failure
                context.ProgressTracker?.RecordStepFailed([result.ResourceId], stepName, (int)sw.ElapsedMilliseconds, ex.Message);
                context.ProgressTracker?.RecordResourceComplete(result.ResourceId, PipelineResourceStatus.Failed, ex.Message, stepName);

                return new[] { PipelineResult<TOutput>.Failure(result.ResourceId, ex.Message, stepName) };
            }
        }, opts);
    }

    /// <summary>
    /// Create a downstream block that groups ordered inputs by key and emits one output per group
    /// </summary>
    public static IPropagatorBlock<PipelineResult<TInput>, PipelineResult<TOutput>> CreateSequentialGroupingBlock<TInput, TOutput, TKey>(
        Func<TInput, TKey> keySelector,
        Func<TKey, IReadOnlyList<TInput>, TOutput> projector,
        Func<TKey, string> getResourceIdFromKey,
        string stepName,
        IPipelineContext context,
        ExecutionDataflowBlockOptions? options = null)
        where TKey : notnull
    {
        if (keySelector is null) throw new ArgumentNullException(nameof(keySelector));
        if (projector is null) throw new ArgumentNullException(nameof(projector));
        if (getResourceIdFromKey is null) throw new ArgumentNullException(nameof(getResourceIdFromKey));

        var opts = options ?? new ExecutionDataflowBlockOptions
        {
            CancellationToken = context.CancellationToken
        };

        // Grouping requires ordered, single-threaded processing to maintain sequence
        opts.MaxDegreeOfParallelism = 1;
        opts.EnsureOrdered = true;

        var outputOptions = new DataflowBlockOptions
        {
            BoundedCapacity = opts.BoundedCapacity,
            CancellationToken = opts.CancellationToken
        };

        var outputBuffer = new BufferBlock<PipelineResult<TOutput>>(outputOptions);
        var comparer = EqualityComparer<TKey>.Default;

        var currentItems = new List<TInput>();
        TKey? currentKey = default;
        var hasCurrent = false;

        async Task EmitGroupAsync(TKey key, IReadOnlyList<TInput> items)
        {
            var resourceId = getResourceIdFromKey(key);
            context.ProgressTracker?.RecordResourceStart(resourceId, context.ResourceTypeName);
            context.ProgressTracker?.RecordStepStart([resourceId], stepName);

            var sw = Stopwatch.StartNew();

            try
            {
                // Create a defensive copy to prevent projector mutations from affecting state
                var snapshot = items.ToArray();
                var output = projector(key, snapshot);
                sw.Stop();

                context.ProgressTracker?.RecordStepComplete([resourceId], stepName, (int)sw.ElapsedMilliseconds);

                var success = PipelineResult<TOutput>.Success(output, resourceId, (int)sw.ElapsedMilliseconds);
                await outputBuffer.SendAsync(success, context.CancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                sw.Stop();
                context.Logger.LogError(ex, "Error grouping sequential resources for step {StepName} and key {Key}", stepName, key);

                context.ProgressTracker?.RecordStepFailed([resourceId], stepName, (int)sw.ElapsedMilliseconds, ex.Message);
                context.ProgressTracker?.RecordResourceComplete(resourceId, PipelineResourceStatus.Failed, ex.Message, stepName);

                var failure = PipelineResult<TOutput>.Failure(resourceId, ex.Message, stepName);
                await outputBuffer.SendAsync(failure, context.CancellationToken).ConfigureAwait(false);
            }
        }

        var actionBlock = new ActionBlock<PipelineResult<TInput>>(async result =>
        {
            if (!result.IsSuccess)
            {
                var failure = PipelineResult<TOutput>.Failure(
                    result.ResourceId,
                    result.ErrorMessage ?? "Previous step failed",
                    result.ErrorStep ?? stepName);

                await outputBuffer.SendAsync(failure, context.CancellationToken).ConfigureAwait(false);
                return;
            }

            var value = result.Value!;
            var key = keySelector(value);

            if (!hasCurrent)
            {
                hasCurrent = true;
                currentKey = key;
                currentItems.Clear();
                currentItems.Add(value);
                return;
            }

            if (comparer.Equals(currentKey!, key))
            {
                currentItems.Add(value);
                return;
            }

            var previousItems = currentItems.ToArray();
            await EmitGroupAsync(currentKey!, previousItems).ConfigureAwait(false);

            currentKey = key;
            currentItems = new List<TInput> { value };
        }, new ExecutionDataflowBlockOptions
        {
            CancellationToken = opts.CancellationToken,
            BoundedCapacity = opts.BoundedCapacity,
            MaxDegreeOfParallelism = 1,
            EnsureOrdered = true
        });

        actionBlock.Completion.ContinueWith(async t =>
        {
            if (t.IsFaulted)
            {
                var ex = t.Exception?.GetBaseException();
                if (ex != null)
                {
                    context.Logger.LogError(ex, "Sequential grouping block faulted for step {StepName}", stepName);
                }
            }

            if (hasCurrent && currentItems.Count > 0)
            {
                var finalItems = currentItems.ToArray();
                await EmitGroupAsync(currentKey!, finalItems).ConfigureAwait(false);
            }

            outputBuffer.Complete();
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default).Unwrap();

        return DataflowBlock.Encapsulate(actionBlock, outputBuffer);
    }
}

