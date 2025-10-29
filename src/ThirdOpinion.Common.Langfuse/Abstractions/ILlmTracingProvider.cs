namespace ThirdOpinion.Common.Langfuse.Abstractions;

/// <summary>
/// Provider-agnostic interface for LLM operation tracing
/// </summary>
public interface ILlmTracingProvider
{
    /// <summary>
    /// Creates a trace for an LLM operation
    /// </summary>
    Task<string> CreateTraceAsync(
        string operationName,
        Dictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records an LLM generation event
    /// </summary>
    Task RecordGenerationAsync(
        string traceId,
        string model,
        object input,
        object? output = null,
        int? inputTokens = null,
        int? outputTokens = null,
        Dictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a span within a trace
    /// </summary>
    Task<string> CreateSpanAsync(
        string traceId,
        string spanName,
        string? parentSpanId = null,
        Dictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes a span
    /// </summary>
    Task CompleteSpanAsync(
        string spanId,
        object? output = null,
        Dictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default);
}