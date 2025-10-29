using System.Text.Json;
using ThirdOpinion.Common.Logging;

namespace ThirdOpinion.Common.Langfuse;

/// <summary>
/// Extension methods and helpers for Bedrock tracing with LangFuse
/// </summary>
public static class BedrockTracingExtensions
{
    /// <summary>
    /// Converts a ModelInvocationRequest to a LangFuse generation request
    /// </summary>
    public static LangfuseGenerationRequest ToLangfuseGeneration<TRequest>(
        this TRequest request,
        string traceId,
        string generationId,
        DateTime startTime,
        string promptName,
        string promptVersion,
        ICorrelationIdProvider? correlationIdProvider = null)
        where TRequest : class
    {
        var metadata = new Dictionary<string, object>
        {
            ["startTime"] = startTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
        };

        if (correlationIdProvider != null)
        {
            metadata["correlationId"] = correlationIdProvider.GetCorrelationId();
        }

        // Use reflection to get common properties
        var requestType = typeof(TRequest);
        var modelIdProp = requestType.GetProperty("ModelId");
        var promptProp = requestType.GetProperty("Prompt");
        var systemPromptProp = requestType.GetProperty("SystemPrompt");
        var stopSequencesProp = requestType.GetProperty("StopSequences");
        var maxTokensProp = requestType.GetProperty("MaxTokens");
        var temperatureProp = requestType.GetProperty("Temperature");
        var topPProp = requestType.GetProperty("TopP");
        var parametersProp = requestType.GetProperty("Parameters");
        var messagesProp = requestType.GetProperty("Messages");
        var toolConfigProp = requestType.GetProperty("ToolConfiguration");

        var modelId = modelIdProp?.GetValue(request)?.ToString() ?? "unknown";
        metadata["modelId"] = modelId;

        var systemPrompt = systemPromptProp?.GetValue(request)?.ToString();
        if (!string.IsNullOrEmpty(systemPrompt))
        {
            metadata["systemPrompt"] = systemPrompt;
        }

        var stopSequences = stopSequencesProp?.GetValue(request) as IEnumerable<string>;
        if (stopSequences?.Any() == true)
        {
            metadata["stopSequences"] = stopSequences.ToArray();
        }

        var toolConfig = toolConfigProp?.GetValue(request);
        if (toolConfig != null)
        {
            try
            {
                var toolConfigJson = JsonSerializer.Serialize(toolConfig);
                metadata["toolConfiguration"] = toolConfigJson;

                // Try to extract tool count if possible
                if (toolConfig.GetType().GetProperty("Tools")?.GetValue(toolConfig) is IEnumerable<object> tools)
                {
                    var toolsList = tools.ToList();
                    if (toolsList.Any())
                    {
                        metadata["toolCount"] = toolsList.Count;
                    }
                }
            }
            catch
            {
                metadata["hasToolConfiguration"] = true;
            }
        }

        // Build model parameters
        var modelParameters = new Dictionary<string, object>();

        var maxTokens = maxTokensProp?.GetValue(request);
        if (maxTokens is int maxTokensInt)
            modelParameters["maxTokens"] = maxTokensInt;
        else if (maxTokens != null && int.TryParse(maxTokens.ToString(), out var parsedMaxTokens))
            modelParameters["maxTokens"] = parsedMaxTokens;

        var temperature = temperatureProp?.GetValue(request);
        if (temperature is double tempDouble)
            modelParameters["temperature"] = tempDouble;
        else if (temperature != null && double.TryParse(temperature.ToString(), out var parsedTemp))
            modelParameters["temperature"] = parsedTemp;

        var topP = topPProp?.GetValue(request);
        if (topP is double topPDouble)
            modelParameters["topP"] = topPDouble;
        else if (topP != null && double.TryParse(topP.ToString(), out var parsedTopP))
            modelParameters["topP"] = parsedTopP;

        // Include any additional parameters
        var parameters = parametersProp?.GetValue(request) as IDictionary<string, object>;
        if (parameters?.Any() == true)
        {
            foreach (var param in parameters)
            {
                if (!modelParameters.ContainsKey(param.Key))
                {
                    modelParameters[param.Key] = param.Value;
                }
            }
        }

        // Build input object
        object input;
        var messages = messagesProp?.GetValue(request) as IEnumerable<object>;
        if (messages?.Any() == true)
        {
            // Use messages format for conversation-style input
            var messageArray = messages.Select(m => new
            {
                role = m.GetType().GetProperty("Role")?.GetValue(m)?.ToString() ?? "user",
                content = m.GetType().GetProperty("Content")?.GetValue(m)?.ToString() ?? ""
            }).ToArray();

            input = new
            {
                messages = messageArray,
                systemPrompt = systemPrompt
            };
        }
        else
        {
            // Use simple prompt format
            var prompt = promptProp?.GetValue(request)?.ToString() ?? "";
            input = new
            {
                prompt = prompt,
                systemPrompt = systemPrompt
            };
        }

        return new LangfuseGenerationRequest
        {
            Id = generationId,
            TraceId = traceId,
            Type = LangfuseObservationTypes.Generation,
            Name = $"{promptName}.{promptVersion}.bedrock-{ExtractModelName(modelId)}",
            StartTime = startTime,
            Model = modelId,
            ModelParameters = modelParameters.Any() ? modelParameters : null,
            Input = input,
            Metadata = metadata,
            Level = LangfuseLevels.Default
        };
    }

    /// <summary>
    /// Updates a LangFuse generation with response data
    /// </summary>
    public static LangfuseGenerationRequest WithResponse<TResponse>(
        this LangfuseGenerationRequest generation,
        TResponse response,
        BedrockCostCalculation? cost = null,
        string? region = null)
        where TResponse : class
    {
        generation.EndTime = DateTime.UtcNow;
        generation.CompletionStartTime = generation.StartTime.AddMilliseconds(100); // Estimate

        // Use reflection to get response properties
        var responseType = typeof(TResponse);
        var contentProp = responseType.GetProperty("Content");
        var usageProp = responseType.GetProperty("Usage");
        var modelIdProp = responseType.GetProperty("ModelId");
        var stopReasonProp = responseType.GetProperty("StopReason");
        var requestIdProp = responseType.GetProperty("RequestId");
        var durationProp = responseType.GetProperty("Duration");

        var content = contentProp?.GetValue(response)?.ToString() ?? "";
        generation.Output = new { content = content };

        // Set usage information
        var usage = usageProp?.GetValue(response);
        if (usage != null)
        {
            var inputTokens = usage.GetType().GetProperty("InputTokens")?.GetValue(usage);
            var outputTokens = usage.GetType().GetProperty("OutputTokens")?.GetValue(usage);
            var totalTokens = usage.GetType().GetProperty("TotalTokens")?.GetValue(usage);

            if (cost != null)
            {
                generation.Usage = new LangfuseUsageWithCost
                {
                    Input = Convert.ToInt32(inputTokens ?? 0),
                    Output = Convert.ToInt32(outputTokens ?? 0),
                    Total = Convert.ToInt32(totalTokens ?? 0),
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
                    Input = Convert.ToInt32(inputTokens ?? 0),
                    Output = Convert.ToInt32(outputTokens ?? 0),
                    Total = Convert.ToInt32(totalTokens ?? 0),
                    Unit = "TOKENS"
                };
            }
        }

        // Update metadata with response information
        var metadata = generation.Metadata ?? new Dictionary<string, object>();
        metadata["endTime"] = generation.EndTime?.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") ?? string.Empty;

        var duration = durationProp?.GetValue(response);
        if (duration is TimeSpan timeSpan)
        {
            metadata["duration"] = timeSpan.TotalMilliseconds;
            metadata["durationMs"] = (int)timeSpan.TotalMilliseconds;
        }

        var stopReason = stopReasonProp?.GetValue(response)?.ToString();
        if (!string.IsNullOrWhiteSpace(stopReason))
        {
            metadata["stopReason"] = stopReason;
        }

        var requestId = requestIdProp?.GetValue(response)?.ToString();
        if (!string.IsNullOrWhiteSpace(requestId))
        {
            metadata["requestId"] = requestId;
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
            var modelPart = cleanId.Substring("anthropic.claude".Length);
            if (modelPart.StartsWith("-"))
                modelPart = modelPart.Substring(1);

            var modelParts = modelPart.Split('-');
            if (modelParts.Length >= 3)
            {
                var modelType = modelParts[0];
                var majorVersion = modelParts[1];
                var minorVersion = modelParts[2];

                if (int.TryParse(majorVersion, out _) && int.TryParse(minorVersion, out _))
                {
                    return $"claude-{modelType}-{majorVersion}-{minorVersion}";
                }
                else if (int.TryParse(modelType, out _) && !int.TryParse(majorVersion, out _))
                {
                    return $"claude-{modelType}-{majorVersion}";
                }
            }

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