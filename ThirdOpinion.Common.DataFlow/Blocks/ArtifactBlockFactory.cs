using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;
using ThirdOpinion.Common.DataFlow.Core;
using ThirdOpinion.Common.DataFlow.Results;
using ThirdOpinion.DataFlow.Artifacts.Models;

namespace ThirdOpinion.Common.DataFlow.Blocks;

/// <summary>
/// Factory for creating blocks with artifact capture capabilities
/// </summary>
public static class ArtifactBlockFactory
{
    /// <summary>
    /// Create a downstream block that captures artifacts
    /// Returns both the processing block and an optional output block that filters artifacts
    /// </summary>
    public static (TransformBlock<PipelineResult<TInput>, PipelineResult<TOutput>> processBlock, 
                   ISourceBlock<PipelineResult<TOutput>>? outputBlock)
        CreateDownstreamBlockWithArtifacts<TInput, TOutput>(
            Func<TInput, Task<TOutput>> transformAsync,
            string stepName,
            IPipelineContext context,
            ArtifactOptions<TOutput> artifactOptions,
            ExecutionDataflowBlockOptions? options = null)
    {
        // Create the main tracked block
        var processBlock = TrackedBlockFactory.CreateDownstreamTrackedBlock(
            transformAsync,
            stepName,
            context,
            options);

        // If no artifact batcher, return just the process block
        if (context.ArtifactBatcher == null)
        {
            return (processBlock, null);
        }

        // Create an action block to handle artifact saving
        var artifactBlock = new ActionBlock<PipelineResult<TOutput>>(async result =>
        {
            if (!result.IsSuccess || result.Value == null)
                return;

            try
            {
                // Determine artifact name
                var artifactName = artifactOptions.ArtifactNameFactory != null
                    ? artifactOptions.ArtifactNameFactory(result.Value)
                    : artifactOptions.ArtifactName ?? $"{stepName}_output";

                // Extract artifact data
                var artifactData = artifactOptions.GetArtifactData != null
                    ? artifactOptions.GetArtifactData(result.Value)
                    : result.Value;

                // Determine resource ID
                var resourceId = artifactOptions.GetResourceId != null
                    ? artifactOptions.GetResourceId(result.Value)
                    : result.ResourceId;

                // Get or create resource run ID if cache available
                Guid resourceRunId;
                if (context.ResourceRunCache != null)
                {
                    resourceRunId = await context.ResourceRunCache.GetOrCreateAsync(
                        context.RunId,
                        resourceId,
                        context.ResourceTypeName,
                        context.CancellationToken);
                }
                else
                {
                    // Generate a deterministic GUID if no cache
                    resourceRunId = Guid.NewGuid();
                }

                // Queue artifact for saving
                var request = new ArtifactSaveRequest
                {
                    ResourceRunId = resourceRunId,
                    StepName = stepName,
                    ArtifactName = artifactName,
                    Data = artifactData,
                    StorageTypeOverride = artifactOptions.StorageType
                };

                await context.ArtifactBatcher.QueueArtifactSaveAsync(request, context.CancellationToken);
            }
            catch (Exception ex)
            {
                context.Logger.LogError(ex, "Error queuing artifact for step {StepName}, resource {ResourceId}", 
                    stepName, result.ResourceId);
            }
        }, new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = DataflowBlockOptions.Unbounded,
            CancellationToken = context.CancellationToken
        });

        // Create a broadcast block to send results to both artifact saving and downstream
        var broadcastBlock = new BroadcastBlock<PipelineResult<TOutput>>(
            result => result, // No cloning needed for immutable results
            new DataflowBlockOptions { CancellationToken = context.CancellationToken });

        // Link process block to broadcast block
        processBlock.LinkTo(broadcastBlock, new DataflowLinkOptions { PropagateCompletion = true });

        // Link broadcast to artifact block (fire and forget)
        broadcastBlock.LinkTo(artifactBlock, new DataflowLinkOptions { PropagateCompletion = false });

        return (processBlock, broadcastBlock);
    }

    /// <summary>
    /// Create an initial block with artifact capture (first step in pipeline)
    /// </summary>
    public static (TransformBlock<TInput, PipelineResult<TOutput>> processBlock,
                   ISourceBlock<PipelineResult<TOutput>>? outputBlock)
        CreateInitialBlockWithArtifacts<TInput, TOutput>(
            Func<TInput, Task<TOutput>> transformAsync,
            Func<TInput, string> getResourceId,
            string stepName,
            IPipelineContext context,
            ArtifactOptions<TOutput> artifactOptions,
            ExecutionDataflowBlockOptions? options = null)
    {
        // Create the main tracked block
        var processBlock = TrackedBlockFactory.CreateInitialTrackedBlock(
            transformAsync,
            getResourceId,
            stepName,
            context,
            options);

        // If no artifact batcher, return just the process block
        if (context.ArtifactBatcher == null)
        {
            return (processBlock, null);
        }

        // Create an action block to handle artifact saving
        var artifactBlock = new ActionBlock<PipelineResult<TOutput>>(async result =>
        {
            if (!result.IsSuccess || result.Value == null)
                return;

            try
            {
                // Determine artifact name
                var artifactName = artifactOptions.ArtifactNameFactory != null
                    ? artifactOptions.ArtifactNameFactory(result.Value)
                    : artifactOptions.ArtifactName ?? $"{stepName}_output";

                // Extract artifact data
                var artifactData = artifactOptions.GetArtifactData != null
                    ? artifactOptions.GetArtifactData(result.Value)
                    : result.Value;

                // Determine resource ID
                var resourceId = artifactOptions.GetResourceId != null
                    ? artifactOptions.GetResourceId(result.Value)
                    : result.ResourceId;

                // Get or create resource run ID if cache available
                Guid resourceRunId;
                if (context.ResourceRunCache != null)
                {
                    resourceRunId = await context.ResourceRunCache.GetOrCreateAsync(
                        context.RunId,
                        resourceId,
                        context.ResourceTypeName,
                        context.CancellationToken);
                }
                else
                {
                    // Generate a deterministic GUID if no cache
                    resourceRunId = Guid.NewGuid();
                }

                // Queue artifact for saving
                var request = new ArtifactSaveRequest
                {
                    ResourceRunId = resourceRunId,
                    StepName = stepName,
                    ArtifactName = artifactName,
                    Data = artifactData,
                    StorageTypeOverride = artifactOptions.StorageType
                };

                await context.ArtifactBatcher.QueueArtifactSaveAsync(request, context.CancellationToken);
            }
            catch (Exception ex)
            {
                context.Logger.LogError(ex, "Error queuing artifact for step {StepName}, resource {ResourceId}", 
                    stepName, result.ResourceId);
            }
        }, new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = DataflowBlockOptions.Unbounded,
            CancellationToken = context.CancellationToken
        });

        // Create a broadcast block to send results to both artifact saving and downstream
        var broadcastBlock = new BroadcastBlock<PipelineResult<TOutput>>(
            result => result, // No cloning needed for immutable results
            new DataflowBlockOptions { CancellationToken = context.CancellationToken });

        // Link process block to broadcast block
        processBlock.LinkTo(broadcastBlock, new DataflowLinkOptions { PropagateCompletion = true });

        // Link broadcast to artifact block (fire and forget)
        broadcastBlock.LinkTo(artifactBlock, new DataflowLinkOptions { PropagateCompletion = false });

        return (processBlock, broadcastBlock);
    }
}

