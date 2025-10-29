namespace ThirdOpinion.Common.Langfuse;

/// <summary>
/// Service for interacting with Langfuse API for prompt management and observability
/// </summary>
public interface ILangfuseService
{
    /// <summary>
    /// Lists all available prompts from Langfuse with optional filtering and pagination
    /// </summary>
    /// <param name="page">Page number for pagination (1-based, default: 1)</param>
    /// <param name="limit">Number of items per page (default: 50, max: 1000)</param>
    /// <param name="name">Filter by prompt name (exact match)</param>
    /// <param name="label">Filter by prompt label</param>
    /// <param name="tag">Filter by prompt tag</param>
    /// <param name="fromCreatedAt">Filter prompts created after this date</param>
    /// <param name="toCreatedAt">Filter prompts created before this date</param>
    /// <param name="version">Filter by specific version number</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of available prompts with pagination metadata</returns>
    Task<LangfusePromptListResponse?> ListPromptsAsync(
        int? page = null,
        int? limit = null,
        string? name = null,
        string? label = null,
        string? tag = null,
        DateTime? fromCreatedAt = null,
        DateTime? toCreatedAt = null,
        int? version = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a prompt definition by name from Langfuse
    /// </summary>
    /// <param name="promptName">Name of the prompt to retrieve</param>
    /// <param name="version">Optional version of the prompt (defaults to latest)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Prompt definition response</returns>
    Task<LangfusePromptResponse?> GetPromptAsync(string promptName, string? version = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new trace for tracking LLM interactions
    /// </summary>
    /// <param name="traceId">Unique identifier for the trace</param>
    /// <param name="name">Human-readable name for the trace</param>
    /// <param name="metadata">Optional metadata to attach to the trace</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created trace response</returns>
    Task<LangfuseTraceResponse?> CreateTraceAsync(string traceId, string name, Dictionary<string, object>? metadata = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an observation within a trace to track individual LLM calls
    /// </summary>
    /// <param name="traceId">ID of the parent trace</param>
    /// <param name="observationId">Unique identifier for the observation</param>
    /// <param name="type">Type of observation (e.g., "generation", "span")</param>
    /// <param name="name">Human-readable name for the observation</param>
    /// <param name="input">Input data for the observation</param>
    /// <param name="output">Output data from the observation</param>
    /// <param name="metadata">Optional metadata</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created observation response</returns>
    Task<LangfuseObservationResponse?> CreateObservationAsync(
        string traceId,
        string observationId,
        string type,
        string name,
        object? input = null,
        object? output = null,
        Dictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing observation with completion data
    /// </summary>
    /// <param name="observationId">ID of the observation to update</param>
    /// <param name="output">Output data from the observation</param>
    /// <param name="usage">Token usage information</param>
    /// <param name="metadata">Additional metadata</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated observation response</returns>
    Task<LangfuseObservationResponse?> UpdateObservationAsync(
        string observationId,
        object? output = null,
        LangfuseUsage? usage = null,
        Dictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Flushes any pending telemetry data to Langfuse
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task FlushAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists observations from Langfuse with optional filtering
    /// </summary>
    /// <param name="page">Page number for pagination (1-based, default: 1)</param>
    /// <param name="limit">Number of items per page (default: 50, max: 1000)</param>
    /// <param name="name">Filter by observation name</param>
    /// <param name="type">Filter by observation type (e.g., "generation", "span")</param>
    /// <param name="traceId">Filter by specific trace ID</param>
    /// <param name="fromStartTime">Filter observations started after this time</param>
    /// <param name="toStartTime">Filter observations started before this time</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of observations with pagination metadata</returns>
    Task<LangfuseObservationListResponse?> ListObservationsAsync(
        int? page = null,
        int? limit = null,
        string? name = null,
        string? type = null,
        string? traceId = null,
        DateTime? fromStartTime = null,
        DateTime? toStartTime = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves traces associated with a specific prompt by analyzing observations
    /// </summary>
    /// <param name="promptName">Name of the prompt to find traces for</param>
    /// <param name="page">Page number for pagination (default: 1)</param>
    /// <param name="limit">Number of traces to return (default: 50)</param>
    /// <param name="fromDate">Optional start date filter</param>
    /// <param name="toDate">Optional end date filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of trace details with associated metadata</returns>
    Task<List<LangfuseTraceDetail>> GetTracesByPromptAsync(
        string promptName,
        int? page = null,
        int? limit = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default);

    // =================== BEDROCK TRACING METHODS ===================

    /// <summary>
    /// Creates a generation observation for LLM calls with enhanced tracking
    /// </summary>
    /// <param name="request">Generation request with all observation details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created generation response</returns>
    Task<LangfuseObservationResponse?> CreateGenerationAsync(
        LangfuseGenerationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a batch of events to LangFuse ingestion API for efficient processing
    /// </summary>
    /// <param name="batch">Batch of events to ingest</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Ingestion response with success/error details</returns>
    Task<LangfuseIngestionResponse?> SendIngestionBatchAsync(
        LangfuseIngestionBatch batch,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an enhanced trace with Bedrock-specific context
    /// </summary>
    /// <param name="request">Enhanced trace request with additional context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created trace response</returns>
    Task<LangfuseTraceResponse?> CreateBedrockTraceAsync(
        LangfuseBedrockTraceRequest request,
        CancellationToken cancellationToken = default);


    /// <summary>
    /// Creates a complete trace and generation for a Bedrock call in a single batch operation
    /// </summary>
    /// <param name="traceRequest">Trace creation request</param>
    /// <param name="generationRequest">Generation creation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Ingestion response with both trace and generation results</returns>
    Task<LangfuseIngestionResponse?> CreateBedrockTraceAndGenerationAsync(
        LangfuseBedrockTraceRequest traceRequest,
        LangfuseGenerationRequest generationRequest,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a generation observation with completion data and costs
    /// </summary>
    /// <param name="generationId">ID of the generation to update</param>
    /// <param name="endTime">Completion time</param>
    /// <param name="output">Model output</param>
    /// <param name="usage">Token usage with cost information</param>
    /// <param name="metadata">Additional metadata</param>
    /// <param name="level">Log level (default, error, warning)</param>
    /// <param name="statusMessage">Optional status message</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated observation response</returns>
    Task<LangfuseObservationResponse?> UpdateGenerationAsync(
        string generationId,
        DateTime? endTime = null,
        object? output = null,
        LangfuseUsage? usage = null,
        Dictionary<string, object>? metadata = null,
        string? level = null,
        string? statusMessage = null,
        CancellationToken cancellationToken = default);
}