using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ThirdOpinion.Common.IA.Pipelines.Artifacts;
using ThirdOpinion.Common.IA.Pipelines.Models;
using ThirdOpinion.Common.IA.Pipelines.Progress;
using ThirdOpinion.Common.IA.Pipelines.Progress.Models;

namespace ThirdOpinion.Common.IA.Pipelines.Core;

/// <summary>
/// Fluent builder for creating IPipelineContext
/// </summary>
public class PipelineContextBuilder
{
    private readonly Type _resourceType;
    private readonly IPipelineProgressTrackerFactory? _progressTrackerFactory;
    private readonly IArtifactBatcherFactory? _artifactBatcherFactory;
    private readonly IResourceRunCache? _resourceRunCache;
    private readonly ILogger _logger;
    
    private PipelineRunMetadata _metadata = new();
    private CancellationToken _cancellationToken = CancellationToken.None;
    private PipelineStepOptions _defaultStepOptions = new();

    /// <summary>
    /// Constructor used by IPipelineContextFactory (public for testing)
    /// </summary>
    public PipelineContextBuilder(
        Type resourceType,
        IPipelineProgressTrackerFactory? progressTrackerFactory = null,
        IArtifactBatcherFactory? artifactBatcherFactory = null,
        IResourceRunCache? resourceRunCache = null,
        ILogger? logger = null)
    {
        _resourceType = resourceType ?? throw new ArgumentNullException(nameof(resourceType));
        _progressTrackerFactory = progressTrackerFactory;
        _artifactBatcherFactory = artifactBatcherFactory;
        _resourceRunCache = resourceRunCache;
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>
    /// Set the run metadata (RunId, Category, Name)
    /// </summary>
    public PipelineContextBuilder WithRunMetadata(PipelineRunMetadata metadata)
    {
        _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        return this;
    }

    /// <summary>
    /// Set the run ID. If not set, a new Guid will be generated.
    /// </summary>
    public PipelineContextBuilder WithRunId(Guid runId)
    {
        _metadata.RunId = runId;
        return this;
    }

    /// <summary>
    /// Set the pipeline category (e.g., "LabResults", "ClinicalFactExtraction")
    /// </summary>
    public PipelineContextBuilder WithCategory(string category)
    {
        _metadata.Category = category ?? throw new ArgumentNullException(nameof(category));
        return this;
    }

    /// <summary>
    /// Set the pipeline name (e.g., "TestosteroneLabAnalysis")
    /// </summary>
    public PipelineContextBuilder WithName(string name)
    {
        _metadata.Name = name ?? throw new ArgumentNullException(nameof(name));
        return this;
    }

    /// <summary>
    /// Set the run type (Fresh, Retry, Continuation, etc.)
    /// </summary>
    public PipelineContextBuilder WithRunType(PipelineRunType runType)
    {
        _metadata.RunType = runType;
        return this;
    }

    /// <summary>
    /// Set the parent run identifier (used for retry/continuation scenarios)
    /// </summary>
    public PipelineContextBuilder WithParentRunId(Guid? parentRunId)
    {
        _metadata.ParentRunId = parentRunId;
        return this;
    }

    /// <summary>
    /// Add a cancellation token
    /// </summary>
    public PipelineContextBuilder WithCancellationToken(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        return this;
    }

    /// <summary>
    /// Set the default step options applied when steps do not specify their own options
    /// </summary>
    public PipelineContextBuilder WithDefaultStepOptions(PipelineStepOptions options)
    {
        if (options is null)
            throw new ArgumentNullException(nameof(options));

        _defaultStepOptions = options.Clone();
        return this;
    }

    /// <summary>
    /// Convenience helper to set the default maximum degree of parallelism for the pipeline
    /// </summary>
    public PipelineContextBuilder WithDefaultMaxDegreeOfParallelism(int maxDegreeOfParallelism)
    {
        if (maxDegreeOfParallelism <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism), "Value must be greater than zero.");

        var stepOptions = _defaultStepOptions.Clone();
        stepOptions.MaxDegreeOfParallelism = maxDegreeOfParallelism;
        _defaultStepOptions = stepOptions;
        return this;
    }

    /// <summary>
    /// Build the pipeline context
    /// </summary>
    public IPipelineContext Build()
    {
        // Ensure RunId is set (generate if not provided)
        var effectiveRunId = _metadata.GetOrCreateRunId();
        
        // Create progress tracker if factory is available
        IPipelineProgressTracker? progressTracker = null;
        if (_progressTrackerFactory != null)
        {
            // Ensure RunId is set in metadata for factory
            _metadata.RunId = effectiveRunId;
            progressTracker = _progressTrackerFactory.Create(_metadata, _cancellationToken);
        }

        IArtifactBatcher? artifactBatcher = null;
        if (_artifactBatcherFactory != null)
        {
            // Provide explicit metadata copy to avoid mutating builder state
            var metadataForFactory = new PipelineRunMetadata
            {
                RunId = effectiveRunId,
                Category = _metadata.Category,
                Name = _metadata.Name,
                RunType = _metadata.RunType,
                ParentRunId = _metadata.ParentRunId
            };

            artifactBatcher = _artifactBatcherFactory.Create(metadataForFactory, _cancellationToken);
        }

        var context = new PipelineContext(
            effectiveRunId,
            _resourceType,
            _cancellationToken,
            _logger,
            progressTracker,
            artifactBatcher,
            _resourceRunCache,
            _defaultStepOptions,
            _metadata.Category,
            _metadata.Name,
            _metadata.RunType,
            _metadata.ParentRunId);

        return context;
    }
}

