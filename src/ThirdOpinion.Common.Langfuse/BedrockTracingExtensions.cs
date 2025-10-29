using System.Text.Json;
using FhirTools.Bedrock;
using FhirTools.Logging;

namespace FhirTools.Langfuse;

/// <summary>
/// Extension methods and helpers for Bedrock tracing with LangFuse
/// </summary>
public static class BedrockTracingExtensions
{
    /// <summary>
    /// Converts a ModelInvocationRequest to a LangFuse generation request
    /// </summary>
    public static LangfuseGenerationRequest ToLangfuseGeneration(
        this ModelInvocationRequest request,
        string traceId,
        string generationId,
        DateTime startTime,
        string promptName,
        string promptVersion,
        ICorrelationIdProvider? correlationIdProvider = null)
    {
        var metadata = new Dictionary<string, object>
        {
            ["modelId"] = request.ModelId,
            ["startTime"] = startTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
        };

        if (correlationIdProvider != null)
        {
            metadata["correlationId"] = correlationIdProvider.GetCorrelationId();
        }

        if (request.SystemPrompt != null)
        {
            metadata["systemPrompt"] = request.SystemPrompt;
        }

        if (request.StopSequences?.Any() == true)
        {
            metadata["stopSequences"] = request.StopSequences;
        }

        if (request.ToolConfiguration?.Tools?.Any() == true)
        {
            metadata["toolCount"] = request.ToolConfiguration.Tools.Count;
            metadata["toolNames"] = request.ToolConfiguration.Tools
                .Where(t => t.ToolSpec?.Name != null)
                .Select(t => t.ToolSpec.Name)
                .ToArray();
        }

        // Build model parameters
        var modelParameters = new Dictionary<string, object>();
        if (request.MaxTokens.HasValue)
            modelParameters["maxTokens"] = request.MaxTokens.Value;
        if (request.Temperature.HasValue)
            modelParameters["temperature"] = request.Temperature.Value;
        if (request.TopP.HasValue)
            modelParameters["topP"] = request.TopP.Value;

        // Include any additional parameters
        if (request.Parameters?.Any() == true)
        {
            foreach (var param in request.Parameters)
            {
                if (!modelParameters.ContainsKey(param.Key))
                {
                    modelParameters[param.Key] = param.Value;
                }
            }
        }

        // Build input object
        object input;
        if (request.Messages?.Any() == true)
        {
            // Use messages format for conversation-style input
            input = new
            {
                messages = request.Messages.Select(m => new { role = m.Role, content = m.Content }).ToArray(),
                systemPrompt = request.SystemPrompt
            };
        }
        else
        {
            // Use simple prompt format
            input = new
            {
                prompt = request.Prompt,
                systemPrompt = request.SystemPrompt
            };
        }

        return new LangfuseGenerationRequest
        {
            Id = generationId,
            TraceId = traceId,
            Type = LangfuseObservationTypes.Generation,
            Name = $"{promptName}.{promptVersion}.bedrock-{ExtractModelName(request.ModelId)}",
            StartTime = startTime,
            Model = request.ModelId,
            ModelParameters = modelParameters.Any() ? modelParameters : null,
            Input = input,
            Metadata = metadata,
            Level = LangfuseLevels.Default
        };
    }

    /// <summary>
    /// Updates a LangFuse generation with response data
    /// </summary>
    public static LangfuseGenerationRequest WithResponse(
        this LangfuseGenerationRequest generation,
        ModelInvocationResponse response,
        BedrockCostCalculation? cost = null,
        string? region = null)
    {
        generation.EndTime = DateTime.UtcNow;
        generation.CompletionStartTime = generation.StartTime.AddMilliseconds(100); // Estimate
        generation.Output = new { content = response.Content };

        // Set usage information
        if (response.Usage != null)
        {
            if (cost != null)
            {
                generation.Usage = new LangfuseUsageWithCost
                {
                    Input = response.Usage.InputTokens,
                    Output = response.Usage.OutputTokens,
                    Total = response.Usage.TotalTokens,
                    Unit = "TOKENS",
                    InputCost = cost.InputCost,
                    OutputCost = cost.OutputCost,
                    TotalCost = cost.TotalCost
                };
            }
            else
            {
                generation.Usage = new LangfuseUsage
                {
                    Input = response.Usage.InputTokens,
                    Output = response.Usage.OutputTokens,
                    Total = response.Usage.TotalTokens,
                    Unit = "TOKENS"
                };
            }
        }

        // Update metadata with response information
        var metadata = generation.Metadata ?? new Dictionary<string, object>();
        metadata["endTime"] = generation.EndTime?.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") ?? string.Empty;
        metadata["duration"] = response.Duration.TotalMilliseconds;
        metadata["durationMs"] = (int)response.Duration.TotalMilliseconds;

        if (!string.IsNullOrWhiteSpace(response.StopReason))
        {
            metadata["stopReason"] = response.StopReason;
        }

        if (!string.IsNullOrWhiteSpace(response.RequestId))
        {
            metadata["requestId"] = response.RequestId;
        }

        if (!string.IsNullOrWhiteSpace(region))
        {
            metadata["region"] = region;
        }

        if (cost != null)
        {
            metadata["cost"] = new
            {
                input = cost.InputCost,
                output = cost.OutputCost,
                total = cost.TotalCost,
                currency = cost.Currency
            };
        }

        generation.Metadata = metadata;
        generation.Level = LangfuseLevels.Default;

        return generation;
    }

    /// <summary>
    /// Creates a LangFuse trace request from Bedrock context
    /// </summary>
    public static LangfuseBedrockTraceRequest CreateBedrockTrace(
        string traceId,
        string traceName,
        BedrockTraceMetadata? traceMetadata = null,
        object? input = null,
        string? userId = null)
    {
        var metadata = new Dictionary<string, object>
        {
            ["source"] = "bedrock",
            ["timestamp"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
        };

        if (traceMetadata != null)
        {
            // Add all custom metadata from the generic dictionary
            foreach (var kvp in traceMetadata.Metadata)
            {
                if (!string.IsNullOrWhiteSpace(kvp.Value))
                {
                    metadata[kvp.Key] = kvp.Value;
                }
            }

            // Add specific trace metadata properties
            if (!string.IsNullOrWhiteSpace(traceMetadata.PromptName))
                metadata["promptName"] = traceMetadata.PromptName;
            if (!string.IsNullOrWhiteSpace(traceMetadata.PromptVersion))
                metadata["promptVersion"] = traceMetadata.PromptVersion;
            if (!string.IsNullOrWhiteSpace(traceMetadata.CorrelationId))
                metadata["correlationId"] = traceMetadata.CorrelationId;
            if (!string.IsNullOrWhiteSpace(traceMetadata.Region))
                metadata["region"] = traceMetadata.Region;
            if (!string.IsNullOrWhiteSpace(traceMetadata.Environment))
                metadata["environment"] = traceMetadata.Environment;
        }

        return new LangfuseBedrockTraceRequest
        {
            Id = traceId,
            Name = traceName,
            Timestamp = DateTime.UtcNow,
            Input = input,
            UserId = userId,
            SessionId = traceMetadata?.DocumentId, // Use document ID as session
            Metadata = metadata,
            Tags = new List<string> { "bedrock", "llm", "generation" },
            Version = "1.0"
        };
    }

    /// <summary>
    /// Creates ingestion events for trace and generation
    /// </summary>
    public static LangfuseIngestionBatch CreateIngestionBatch(
        LangfuseBedrockTraceRequest trace,
        LangfuseGenerationRequest generation)
    {
        var events = new List<LangfuseIngestionEvent>
        {
            new LangfuseIngestionEvent
            {
                Id = $"trace-{Guid.NewGuid()}",
                Type = LangfuseEventTypes.TraceCreate,
                Timestamp = trace.Timestamp,
                Body = trace
            },
            new LangfuseIngestionEvent
            {
                Id = $"generation-{Guid.NewGuid()}",
                Type = LangfuseEventTypes.GenerationCreate,
                Timestamp = generation.StartTime,
                Body = generation
            }
        };

        return new LangfuseIngestionBatch { Batch = events };
    }

    /// <summary>
    /// Updates trace with final output and completion status
    /// </summary>
    public static LangfuseBedrockTraceRequest WithOutput(
        this LangfuseBedrockTraceRequest trace,
        object? output,
        bool success = true,
        string? errorMessage = null)
    {
        trace.Output = output;

        var metadata = trace.Metadata ?? new Dictionary<string, object>();
        metadata["completedAt"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        metadata["success"] = success;

        if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            metadata["error"] = errorMessage;
        }

        trace.Metadata = metadata;

        return trace;
    }

    /// <summary>
    /// Marks a generation as failed with error details
    /// </summary>
    public static LangfuseGenerationRequest WithError(
        this LangfuseGenerationRequest generation,
        Exception exception,
        DateTime? endTime = null)
    {
        generation.EndTime = endTime ?? DateTime.UtcNow;
        generation.Level = LangfuseLevels.Error;
        generation.StatusMessage = exception.Message;
        generation.Output = new
        {
            error = exception.Message,
            errorType = exception.GetType().Name,
            stackTrace = exception.StackTrace
        };

        var metadata = generation.Metadata ?? new Dictionary<string, object>();
        metadata["error"] = true;
        metadata["errorMessage"] = exception.Message;
        metadata["errorType"] = exception.GetType().Name;
        metadata["endTime"] = generation.EndTime?.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") ?? string.Empty;

        generation.Metadata = metadata;

        return generation;
    }

    /// <summary>
    /// Extracts a readable model name from the full Bedrock model ID
    /// </summary>
    public static string ExtractModelName(string modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
            return "unknown";

        // Remove region prefixes
        var cleanId = modelId;
        var prefixes = new[] { "us.", "eu.", "ap.", "ca." };
        foreach (var prefix in prefixes)
        {
            if (cleanId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                cleanId = cleanId.Substring(prefix.Length);
                break;
            }
        }

        // Extract provider and model name
        if (cleanId.StartsWith("anthropic.claude", StringComparison.OrdinalIgnoreCase))
        {
            if (cleanId.Contains("4-5") || cleanId.Contains("4.5"))
                return "claude-4.5";
            if (cleanId.Contains("4-1") || cleanId.Contains("4.1"))
                return "claude-4.1";
            if (cleanId.Contains("3-5") || cleanId.Contains("3.5"))
                return "claude-3.5";
            if (cleanId.Contains("haiku"))
                return "claude-haiku";
            if (cleanId.Contains("sonnet"))
                return "claude-sonnet";
            if (cleanId.Contains("opus"))
                return "claude-opus";
            return "claude";
        }

        if (cleanId.StartsWith("amazon.nova", StringComparison.OrdinalIgnoreCase))
        {
            if (cleanId.Contains("pro"))
                return "nova-pro";
            if (cleanId.Contains("lite"))
                return "nova-lite";
            if (cleanId.Contains("micro"))
                return "nova-micro";
            if (cleanId.Contains("premier"))
                return "nova-premier";
            return "nova";
        }

        if (cleanId.StartsWith("amazon.titan", StringComparison.OrdinalIgnoreCase))
        {
            if (cleanId.Contains("express"))
                return "titan-express";
            if (cleanId.Contains("lite"))
                return "titan-lite";
            if (cleanId.Contains("embed"))
                return "titan-embed";
            return "titan";
        }

        if (cleanId.StartsWith("meta.llama", StringComparison.OrdinalIgnoreCase))
        {
            if (cleanId.Contains("3-2") || cleanId.Contains("3.2"))
                return "llama-3.2";
            if (cleanId.Contains("70b"))
                return "llama-70b";
            if (cleanId.Contains("13b"))
                return "llama-13b";
            if (cleanId.Contains("7b"))
                return "llama-7b";
            return "llama";
        }

        // Fallback: use the first part after the provider
        var parts = cleanId.Split('.', '-');
        if (parts.Length >= 2)
        {
            return $"{parts[0]}-{parts[1]}";
        }

        return cleanId.Length > 20 ? cleanId.Substring(0, 20) : cleanId;
    }

    /// <summary>
    /// Safely converts an object to JSON string for metadata
    /// </summary>
    public static string ToJsonString(this object obj)
    {
        try
        {
            return JsonSerializer.Serialize(obj, new JsonSerializerOptions
            {
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
        }
        catch
        {
            return obj?.ToString() ?? string.Empty;
        }
    }

    /// <summary>
    /// Creates a session ID from document and practice context
    /// </summary>
    public static string CreateSessionId(string? documentId = null, string? practiceId = null)
    {
        if (!string.IsNullOrWhiteSpace(documentId) && !string.IsNullOrWhiteSpace(practiceId))
        {
            return $"{practiceId}-{documentId}";
        }

        if (!string.IsNullOrWhiteSpace(documentId))
        {
            return documentId;
        }

        if (!string.IsNullOrWhiteSpace(practiceId))
        {
            return practiceId;
        }

        return Guid.NewGuid().ToString("N")[..16]; // Short session ID
    }

    /// <summary>
    /// Creates a user ID from available context
    /// </summary>
    public static string CreateUserId(string? practiceId = null, string? patientId = null)
    {
        if (!string.IsNullOrWhiteSpace(practiceId) && !string.IsNullOrWhiteSpace(patientId))
        {
            return $"{practiceId}:{patientId}";
        }

        if (!string.IsNullOrWhiteSpace(practiceId))
        {
            return practiceId;
        }

        if (!string.IsNullOrWhiteSpace(patientId))
        {
            return patientId;
        }

        return "system";
    }
}