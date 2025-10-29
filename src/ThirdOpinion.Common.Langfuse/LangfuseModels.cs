using System.Text.Json.Serialization;
using System.Text.Json;
using Amazon.Runtime.Documents;

namespace ThirdOpinion.Common.Langfuse;

/// <summary>
/// Response model for Langfuse prompt list API
/// </summary>
public class LangfusePromptListResponse
{
    [JsonPropertyName("data")]
    public List<LangfusePromptResponse> Data { get; set; } = new();

    [JsonPropertyName("meta")]
    public LangfuseMetadata? Meta { get; set; }
}

/// <summary>
/// Metadata for paginated responses
/// </summary>
public class LangfuseMetadata
{
    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("limit")]
    public int Limit { get; set; }

    [JsonPropertyName("totalItems")]
    public int TotalItems { get; set; }

    [JsonPropertyName("totalPages")]
    public int TotalPages { get; set; }
}

/// <summary>
/// Response model for Langfuse prompt API
/// </summary>
public class LangfusePromptResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = string.Empty;

    [JsonPropertyName("config")]
    public Dictionary<string, object>? Config { get; set; }

    [JsonPropertyName("labels")]
    public List<string>? Labels { get; set; }

    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Response model for Langfuse trace API
/// </summary>
public class LangfuseTraceResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }
}

/// <summary>
/// Response model for Langfuse observation API
/// </summary>
public class LangfuseObservationResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("traceId")]
    public string TraceId { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("startTime")]
    public DateTime StartTime { get; set; }

    [JsonPropertyName("endTime")]
    public DateTime? EndTime { get; set; }

    [JsonPropertyName("input")]
    public object? Input { get; set; }

    [JsonPropertyName("output")]
    public object? Output { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }

    [JsonPropertyName("usage")]
    public LangfuseUsage? Usage { get; set; }
}

/// <summary>
/// Model for tracking token usage in LLM calls
/// </summary>
public class LangfuseUsage
{
    [JsonPropertyName("input")]
    public int? Input { get; set; }

    [JsonPropertyName("output")]
    public int? Output { get; set; }

    [JsonPropertyName("total")]
    public int? Total { get; set; }

    [JsonPropertyName("unit")]
    public string Unit { get; set; } = "TOKENS";
}

/// <summary>
/// Request model for creating traces
/// </summary>
public class LangfuseTraceRequest
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }
}

/// <summary>
/// Request model for creating observations
/// </summary>
public class LangfuseObservationRequest
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("traceId")]
    public string TraceId { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("startTime")]
    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("endTime")]
    public DateTime? EndTime { get; set; }

    [JsonPropertyName("input")]
    public object? Input { get; set; }

    [JsonPropertyName("output")]
    public object? Output { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }

    [JsonPropertyName("usage")]
    public LangfuseUsage? Usage { get; set; }
}

/// <summary>
/// Request model for updating observations
/// </summary>
public class LangfuseObservationUpdateRequest
{
    [JsonPropertyName("endTime")]
    public DateTime? EndTime { get; set; }

    [JsonPropertyName("output")]
    public object? Output { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }

    [JsonPropertyName("usage")]
    public LangfuseUsage? Usage { get; set; }
}

/// <summary>
/// Helper class for building query parameters for Langfuse API requests
/// </summary>
public static class LangfuseQueryBuilder
{
    /// <summary>
    /// Builds query string for the prompts list endpoint
    /// </summary>
    public static string BuildPromptsQuery(
        int? page = null,
        int? limit = null,
        string? name = null,
        string? label = null,
        string? tag = null,
        DateTime? fromCreatedAt = null,
        DateTime? toCreatedAt = null,
        int? version = null)
    {
        var queryParams = new List<string>();

        if (page.HasValue && page.Value > 0)
            queryParams.Add($"page={page.Value}");

        if (limit.HasValue && limit.Value > 0 && limit.Value <= 1000)
            queryParams.Add($"limit={limit.Value}");

        if (!string.IsNullOrWhiteSpace(name))
            queryParams.Add($"name={Uri.EscapeDataString(name)}");

        if (!string.IsNullOrWhiteSpace(label))
            queryParams.Add($"label={Uri.EscapeDataString(label)}");

        if (!string.IsNullOrWhiteSpace(tag))
            queryParams.Add($"tag={Uri.EscapeDataString(tag)}");

        if (fromCreatedAt.HasValue)
            queryParams.Add($"fromCreatedAt={Uri.EscapeDataString(fromCreatedAt.Value.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"))}");

        if (toCreatedAt.HasValue)
            queryParams.Add($"toCreatedAt={Uri.EscapeDataString(toCreatedAt.Value.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"))}");

        if (version.HasValue && version.Value > 0)
            queryParams.Add($"version={version.Value}");

        return queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : string.Empty;
    }

    /// <summary>
    /// Builds query string for the observations list endpoint
    /// </summary>
    public static string BuildObservationsQuery(
        int? page = null,
        int? limit = null,
        string? name = null,
        string? type = null,
        string? traceId = null,
        DateTime? fromStartTime = null,
        DateTime? toStartTime = null)
    {
        var queryParams = new List<string>();

        if (page.HasValue && page.Value > 0)
            queryParams.Add($"page={page.Value}");

        if (limit.HasValue && limit.Value > 0 && limit.Value <= 1000)
            queryParams.Add($"limit={limit.Value}");

        if (!string.IsNullOrWhiteSpace(name))
            queryParams.Add($"name={Uri.EscapeDataString(name)}");

        if (!string.IsNullOrWhiteSpace(type))
            queryParams.Add($"type={Uri.EscapeDataString(type)}");

        if (!string.IsNullOrWhiteSpace(traceId))
            queryParams.Add($"traceId={Uri.EscapeDataString(traceId)}");

        if (fromStartTime.HasValue)
            queryParams.Add($"fromStartTime={Uri.EscapeDataString(fromStartTime.Value.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"))}");

        if (toStartTime.HasValue)
            queryParams.Add($"toStartTime={Uri.EscapeDataString(toStartTime.Value.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"))}");

        return queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : string.Empty;
    }
}

/// <summary>
/// Response model for Langfuse observation list API
/// </summary>
public class LangfuseObservationListResponse
{
    [JsonPropertyName("data")]
    public List<LangfuseObservationResponse> Data { get; set; } = new();

    [JsonPropertyName("meta")]
    public LangfuseMetadata? Meta { get; set; }
}

/// <summary>
/// Response model for Langfuse trace list API
/// </summary>
public class LangfuseTraceListResponse
{
    [JsonPropertyName("data")]
    public List<LangfuseTraceResponse> Data { get; set; } = new();

    [JsonPropertyName("meta")]
    public LangfuseMetadata? Meta { get; set; }
}

/// <summary>
/// Enhanced trace response with additional metadata for display
/// </summary>
public class LangfuseTraceDetail
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? PromptName { get; set; }
    public string? Model { get; set; }
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    public int? TotalTokens { get; set; }
    public TimeSpan? Duration { get; set; }
    public string Status { get; set; } = "Unknown";
    public List<string>? Tags { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
    public List<LangfuseObservationResponse> Observations { get; set; } = new();
}

/// <summary>
/// Extended prompt response with schema parsing capabilities
/// </summary>
public class
    LangfusePromptWithSchema : LangfusePromptResponse
{
    /// <summary>
    /// Parsed schema from the config element
    /// </summary>
    public JsonDocument? Schema { get; set; }

    /// <summary>
    /// Tool name extracted from schema or generated from prompt name
    /// </summary>
    public string? ToolName { get; set; }

    /// <summary>
    /// Tool description extracted from schema or generated from prompt
    /// </summary>
    public string? ToolDescription { get; set; }

    // TODO: Implement provider-agnostic tool configuration
    // Removed AWS-specific ToolConfiguration implementation
    /*
    public ToolConfiguration? BuildToolConfiguration()
    {
        // Implementation removed - AWS-specific
        return null;
    }
    */

    /// <summary>
    /// Generates a tool name from the prompt name
    /// </summary>
    private string GenerateToolName()
    {
        if (string.IsNullOrWhiteSpace(Name))
            return "structured_output";

        // Convert prompt name to valid tool name (snake_case, alphanumeric + underscore)
        var toolName = Name.ToLowerInvariant()
            .Replace("-", "_")
            .Replace(" ", "_");

        // Remove invalid characters
        toolName = new string(toolName.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());

        // Ensure it starts with a letter
        if (!char.IsLetter(toolName.FirstOrDefault()))
            toolName = "tool_" + toolName;

        return toolName;
    }

    /// <summary>
    /// Generates a tool description from the prompt or name
    /// </summary>
    private string GenerateToolDescription()
    {
        if (!string.IsNullOrWhiteSpace(Prompt) && Prompt.Length <= 200)
        {
            return $"Structured output tool for: {Prompt.Substring(0, Math.Min(200, Prompt.Length))}";
        }

        return $"Structured output tool for prompt: {Name ?? "unnamed"}";
    }
}

/// <summary>
/// Helper class for schema-related operations
/// </summary>
public static class LangfuseSchemaHelper
{
    /// <summary>
    /// Extracts schema from Langfuse prompt config
    /// </summary>
    public static JsonDocument? ExtractSchemaFromConfig(Dictionary<string, object>? config)
    {
        if (config == null || !config.TryGetValue("schema", out var schemaValue))
            return null;

        try
        {
            string schemaJson;

            if (schemaValue is JsonElement jsonElement)
            {
                schemaJson = jsonElement.GetRawText();
            }
            else if (schemaValue is string schemaString)
            {
                schemaJson = schemaString;
            }
            else
            {
                // Try to serialize the object to JSON
                schemaJson = JsonSerializer.Serialize(schemaValue);
            }

            return JsonDocument.Parse(schemaJson);
        }
        catch (Exception)
        {
            // If parsing fails, return null
            return null;
        }
    }

    /// <summary>
    /// Converts a LangfusePromptResponse to LangfusePromptWithSchema
    /// </summary>
    public static LangfusePromptWithSchema ConvertToPromptWithSchema(LangfusePromptResponse prompt)
    {
        var schema = ExtractSchemaFromConfig(prompt.Config);

        return new LangfusePromptWithSchema
        {
            Id = prompt.Id,
            Name = prompt.Name,
            Version = prompt.Version,
            Prompt = prompt.Prompt,
            Config = prompt.Config,
            Labels = prompt.Labels,
            Tags = prompt.Tags,
            CreatedAt = prompt.CreatedAt,
            UpdatedAt = prompt.UpdatedAt,
            Schema = schema,
            ToolName = ExtractToolNameFromConfig(prompt.Config, prompt.Name),
            ToolDescription = ExtractToolDescriptionFromConfig(prompt.Config, prompt.Prompt)
        };
    }

    /// <summary>
    /// Extracts tool name from config or generates from prompt name
    /// </summary>
    private static string? ExtractToolNameFromConfig(Dictionary<string, object>? config, string promptName)
    {
        if (config?.TryGetValue("toolName", out var toolNameValue) == true)
        {
            return toolNameValue?.ToString();
        }

        return null; // Will use generated name
    }

    /// <summary>
    /// Extracts tool description from config or generates from prompt
    /// </summary>
    private static string? ExtractToolDescriptionFromConfig(Dictionary<string, object>? config, string prompt)
    {
        if (config?.TryGetValue("toolDescription", out var toolDescValue) == true)
        {
            return toolDescValue?.ToString();
        }

        return null; // Will use generated description
    }
}

// =================== BEDROCK TRACING MODELS ===================

/// <summary>
/// Specialized model for creating LLM generation observations in LangFuse
/// </summary>
public class LangfuseGenerationRequest
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("traceId")]
    public string TraceId { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "GENERATION";

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("startTime")]
    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("endTime")]
    public DateTime? EndTime { get; set; }

    [JsonPropertyName("completionStartTime")]
    public DateTime? CompletionStartTime { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("modelParameters")]
    public Dictionary<string, object>? ModelParameters { get; set; }

    [JsonPropertyName("input")]
    public object? Input { get; set; }

    [JsonPropertyName("output")]
    public object? Output { get; set; }

    [JsonPropertyName("usage")]
    public LangfuseUsage? Usage { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }

    [JsonPropertyName("level")]
    public string Level { get; set; } = "DEFAULT";

    [JsonPropertyName("statusMessage")]
    public string? StatusMessage { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }
}

/// <summary>
/// Enhanced usage tracking with cost information
/// </summary>
public class LangfuseUsageWithCost : LangfuseUsage
{
    [JsonPropertyName("inputCost")]
    public decimal? InputCost { get; set; }

    [JsonPropertyName("outputCost")]
    public decimal? OutputCost { get; set; }

    [JsonPropertyName("totalCost")]
    public decimal? TotalCost { get; set; }
}

/// <summary>
/// Request model for LangFuse ingestion batch API
/// </summary>
public class LangfuseIngestionBatch
{
    [JsonPropertyName("batch")]
    public List<LangfuseIngestionEvent> Batch { get; set; } = new();
}

/// <summary>
/// Individual event in LangFuse ingestion batch
/// </summary>
public class LangfuseIngestionEvent
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("body")]
    public object Body { get; set; } = new();
}

/// <summary>
/// Response model for LangFuse ingestion batch API
/// </summary>
public class LangfuseIngestionResponse
{
    [JsonPropertyName("successes")]
    public List<LangfuseIngestionEventResponse> Successes { get; set; } = new();

    [JsonPropertyName("errors")]
    public List<LangfuseIngestionError> Errors { get; set; } = new();
}

/// <summary>
/// Response for individual ingestion events
/// </summary>
public class LangfuseIngestionEventResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public int Status { get; set; }
}

/// <summary>
/// Error information for failed ingestion events
/// </summary>
public class LangfuseIngestionError
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("error")]
    public string Error { get; set; } = string.Empty;
}

/// <summary>
/// Enhanced trace request with Bedrock-specific context
/// </summary>
public class LangfuseBedrockTraceRequest : LangfuseTraceRequest
{
    [JsonPropertyName("input")]
    public object? Input { get; set; }

    [JsonPropertyName("output")]
    public object? Output { get; set; }

    [JsonPropertyName("userId")]
    public string? UserId { get; set; }

    [JsonPropertyName("sessionId")]
    public string? SessionId { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("release")]
    public string? Release { get; set; }

    [JsonPropertyName("public")]
    public bool? Public { get; set; }
}

/// <summary>
/// Model pricing information for cost calculation
/// </summary>
public class BedrockModelPricing
{
    public string ModelId { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public decimal InputTokensPer1K { get; set; }
    public decimal OutputTokensPer1K { get; set; }
    public string Region { get; set; } = "us-east-2";
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Cost calculation result
/// </summary>
public class BedrockCostCalculation
{
    public decimal InputCost { get; set; }
    public decimal OutputCost { get; set; }
    public decimal TotalCost { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int TotalTokens { get; set; }
    public string ModelId { get; set; } = string.Empty;
    public string Currency { get; set; } = "USD";
}

/// <summary>
/// Bedrock-specific metadata for LangFuse traces with generic dictionary support
/// </summary>
public class BedrockTraceMetadata
{
    /// <summary>
    /// Generic metadata dictionary for flexible key-value storage
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// Name of the LangFuse prompt used
    /// </summary>
    public string? PromptName { get; set; }

    /// <summary>
    /// Version of the LangFuse prompt used
    /// </summary>
    public string? PromptVersion { get; set; }

    /// <summary>
    /// Correlation ID for request tracking
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// AWS region where the request was processed
    /// </summary>
    public string? Region { get; set; }

    /// <summary>
    /// Environment (Development, Production, etc.)
    /// </summary>
    public string? Environment { get; set; }

    // Convenience properties for common metadata keys
    public string? DocumentId
    {
        get => Metadata.TryGetValue("documentId", out var value) ? value : null;
        set { if (value != null) Metadata["documentId"] = value; else Metadata.Remove("documentId"); }
    }

    public string? PracticeId
    {
        get => Metadata.TryGetValue("practiceId", out var value) ? value : null;
        set { if (value != null) Metadata["practiceId"] = value; else Metadata.Remove("practiceId"); }
    }

    public string? PatientId
    {
        get => Metadata.TryGetValue("patientId", out var value) ? value : null;
        set { if (value != null) Metadata["patientId"] = value; else Metadata.Remove("patientId"); }
    }

    public string? S3Bucket
    {
        get => Metadata.TryGetValue("s3Bucket", out var value) ? value : null;
        set { if (value != null) Metadata["s3Bucket"] = value; else Metadata.Remove("s3Bucket"); }
    }

    public string? S3Key
    {
        get => Metadata.TryGetValue("s3Key", out var value) ? value : null;
        set { if (value != null) Metadata["s3Key"] = value; else Metadata.Remove("s3Key"); }
    }
}

/// <summary>
/// Constants for LangFuse event types
/// </summary>
public static class LangfuseEventTypes
{
    public const string TraceCreate = "trace-create";
    public const string GenerationCreate = "generation-create";
    public const string GenerationUpdate = "generation-update";
    public const string SpanCreate = "span-create";
    public const string SpanUpdate = "span-update";
    public const string EventCreate = "event-create";
}

/// <summary>
/// Constants for LangFuse observation types
/// </summary>
public static class LangfuseObservationTypes
{
    public const string Generation = "GENERATION";
    public const string Span = "SPAN";
    public const string Event = "EVENT";
}

/// <summary>
/// Constants for LangFuse logging levels
/// </summary>
public static class LangfuseLevels
{
    public const string Default = "DEFAULT";
    public const string Debug = "DEBUG";
    public const string Warning = "WARNING";
    public const string Error = "ERROR";
}