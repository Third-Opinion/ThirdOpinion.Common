using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.BedrockRuntime.Model;

namespace ThirdOpinion.Common.Aws.Bedrock;

/// <summary>
///     Request model for invoking a Bedrock model
/// </summary>
public class ModelInvocationRequest
{
    /// <summary>
    ///     The identifier of the model to invoke
    /// </summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    ///     The prompt to send to the model
    /// </summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>
    ///     Additional parameters for model invocation
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();

    /// <summary>
    ///     Maximum number of tokens to generate
    /// </summary>
    public int? MaxTokens { get; set; }

    /// <summary>
    ///     Temperature for response randomness (0.0 to 1.0)
    /// </summary>
    public double? Temperature { get; set; }

    /// <summary>
    ///     Top-p nucleus sampling parameter
    /// </summary>
    public double? TopP { get; set; }

    /// <summary>
    ///     Stop sequences to halt generation
    /// </summary>
    public List<string>? StopSequences { get; set; }

    /// <summary>
    ///     System prompt for Claude models
    /// </summary>
    public string? SystemPrompt { get; set; }

    /// <summary>
    ///     Messages for conversational models (Claude)
    /// </summary>
    public List<ChatMessage>? Messages { get; set; }

    /// <summary>
    ///     Tool configuration for structured output (Claude models)
    /// </summary>
    public ToolConfiguration? ToolConfiguration { get; set; }
}

/// <summary>
///     Response model from a Bedrock model invocation
/// </summary>
public class ModelInvocationResponse
{
    /// <summary>
    ///     The generated text content
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    ///     Usage statistics for the invocation
    /// </summary>
    public ModelUsage Usage { get; set; } = new();

    /// <summary>
    ///     The model that was invoked
    /// </summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    ///     Stop reason for generation ending
    /// </summary>
    public string? StopReason { get; set; }

    /// <summary>
    ///     Request ID for tracking
    /// </summary>
    public string? RequestId { get; set; }

    /// <summary>
    ///     Duration of the model invocation
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    ///     Raw response body from the model (for debugging)
    /// </summary>
    public string? RawResponse { get; set; }
}

/// <summary>
///     Usage statistics for a model invocation
/// </summary>
public class ModelUsage
{
    /// <summary>
    ///     Number of input tokens
    /// </summary>
    public int InputTokens { get; set; }

    /// <summary>
    ///     Number of output tokens
    /// </summary>
    public int OutputTokens { get; set; }

    /// <summary>
    ///     Total number of tokens
    /// </summary>
    public int TotalTokens => InputTokens + OutputTokens;
}

/// <summary>
///     Chat message for conversational models
/// </summary>
public class ChatMessage
{
    /// <summary>
    ///     Role of the message sender (user, assistant, system)
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    ///     Content of the message
    /// </summary>
    public string Content { get; set; } = string.Empty;
}

/// <summary>
///     Claude-specific request format
/// </summary>
public class ClaudeRequest
{
    [JsonPropertyName("anthropic_version")]
    public string AnthropicVersion { get; set; } = "bedrock-2023-05-31";

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; } = 1000;

    [JsonPropertyName("temperature")]
    public double Temperature { get; set; } = 0.7;

    [JsonPropertyName("top_p")]
    public double? TopP { get; set; }

    [JsonPropertyName("messages")]
    public List<ClaudeMessage> Messages { get; set; } = new();

    [JsonPropertyName("system")]
    public string? System { get; set; }

    [JsonPropertyName("stop_sequences")]
    public List<string>? StopSequences { get; set; }

    [JsonPropertyName("tools")]
    public List<ClaudeTool>? Tools { get; set; }

    [JsonPropertyName("tool_choice")]
    public ClaudeToolChoice? ToolChoice { get; set; }
}

/// <summary>
///     Claude message format
/// </summary>
public class ClaudeMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

/// <summary>
///     Claude response format
/// </summary>
public class ClaudeResponse
{
    [JsonPropertyName("content")]
    public List<ClaudeContent> Content { get; set; } = new();

    [JsonPropertyName("usage")]
    public ClaudeUsage Usage { get; set; } = new();

    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; set; }

    [JsonPropertyName("stop_sequence")]
    public string? StopSequence { get; set; }
}

/// <summary>
///     Claude content block
/// </summary>
public class ClaudeContent
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

/// <summary>
///     Claude usage statistics
/// </summary>
public class ClaudeUsage
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; set; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; set; }
}

/// <summary>
///     Titan text request format
/// </summary>
public class TitanTextRequest
{
    [JsonPropertyName("inputText")]
    public string InputText { get; set; } = string.Empty;

    [JsonPropertyName("textGenerationConfig")]
    public TitanTextGenerationConfig TextGenerationConfig { get; set; } = new();
}

/// <summary>
///     Titan text generation configuration
/// </summary>
public class TitanTextGenerationConfig
{
    [JsonPropertyName("maxTokenCount")]
    public int MaxTokenCount { get; set; } = 1000;

    [JsonPropertyName("temperature")]
    public double Temperature { get; set; } = 0.7;

    [JsonPropertyName("topP")]
    public double TopP { get; set; } = 1.0;

    [JsonPropertyName("stopSequences")]
    public List<string>? StopSequences { get; set; }
}

/// <summary>
///     Titan text response format
/// </summary>
public class TitanTextResponse
{
    [JsonPropertyName("results")]
    public List<TitanResult> Results { get; set; } = new();

    [JsonPropertyName("inputTextTokenCount")]
    public int InputTextTokenCount { get; set; }
}

/// <summary>
///     Titan result format
/// </summary>
public class TitanResult
{
    [JsonPropertyName("tokenCount")]
    public int TokenCount { get; set; }

    [JsonPropertyName("outputText")]
    public string OutputText { get; set; } = string.Empty;

    [JsonPropertyName("completionReason")]
    public string? CompletionReason { get; set; }
}

/// <summary>
///     Known Bedrock model identifiers and utilities
/// </summary>
public static class BedrockModels
{
    /// <summary>
    ///     Determines if a model uses Claude format
    /// </summary>
    public static bool IsClaudeModel(string modelId)
    {
        return modelId.StartsWith("anthropic.claude", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Determines if a model uses Titan format
    /// </summary>
    public static bool IsTitanModel(string modelId)
    {
        return modelId.StartsWith("amazon.titan", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Determines if a model uses Llama format
    /// </summary>
    public static bool IsLlamaModel(string modelId)
    {
        return modelId.StartsWith("meta.llama", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Determines if a model is a supported Claude 4.x model
    /// </summary>
    public static bool IsClaude4Model(string modelId)
    {
        return IsClaudeModel(modelId) && (
            modelId.Contains("claude-4", StringComparison.OrdinalIgnoreCase) ||
            modelId.Contains("claude-sonnet-4", StringComparison.OrdinalIgnoreCase) ||
            modelId.Contains("claude-haiku-4", StringComparison.OrdinalIgnoreCase) ||
            modelId.Contains("claude-opus-4", StringComparison.OrdinalIgnoreCase)
        );
    }

    /// <summary>
    ///     Determines if a model is an unsupported Claude 3.x model
    /// </summary>
    public static bool IsUnsupportedClaude3Model(string modelId)
    {
        return IsClaudeModel(modelId) && !IsClaude4Model(modelId);
    }

    public static class Claude
    {
        // Claude 4.x models only
        public const string Claude4SonnetLatest = "anthropic.claude-sonnet-4-5-20250929-v1:0";
        public const string Claude4HaikuLatest = "anthropic.claude-haiku-4-5-20250929-v1:0";
        public const string Claude4OpusLatest = "anthropic.claude-opus-4-1-20250805-v1:0";
    }

    public static class Titan
    {
        public const string TitanTextExpress = "amazon.titan-text-express-v1";
        public const string TitanTextLite = "amazon.titan-text-lite-v1";
    }

    public static class Llama
    {
        public const string Llama2_7B = "meta.llama2-7b-chat-v1";
        public const string Llama2_13B = "meta.llama2-13b-chat-v1";
        public const string Llama2_70B = "meta.llama2-70b-chat-v1";
    }
}

/// <summary>
///     Information about a Bedrock foundation model
/// </summary>
public class BedrockFoundationModel
{
    /// <summary>
    ///     The model identifier
    /// </summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    ///     The model name
    /// </summary>
    public string ModelName { get; set; } = string.Empty;

    /// <summary>
    ///     The provider of the model (e.g., Anthropic, Amazon, Meta)
    /// </summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>
    ///     Model ARN
    /// </summary>
    public string ModelArn { get; set; } = string.Empty;

    /// <summary>
    ///     Supported input modalities (TEXT, IMAGE, etc.)
    /// </summary>
    public List<string> InputModalities { get; set; } = new();

    /// <summary>
    ///     Supported output modalities (TEXT, IMAGE, etc.)
    /// </summary>
    public List<string> OutputModalities { get; set; } = new();

    /// <summary>
    ///     Whether the model supports streaming
    /// </summary>
    public bool ResponseStreamingSupported { get; set; }

    /// <summary>
    ///     Supported customizations (FINE_TUNING, etc.)
    /// </summary>
    public List<string> Customizations { get; set; } = new();

    /// <summary>
    ///     Supported inference types (ON_DEMAND, PROVISIONED)
    /// </summary>
    public List<string> InferenceTypes { get; set; } = new();
}

/// <summary>
///     Response from listing foundation models
/// </summary>
public class ListFoundationModelsResponse
{
    /// <summary>
    ///     List of available foundation models
    /// </summary>
    public List<BedrockFoundationModel> ModelSummaries { get; set; } = new();

    /// <summary>
    ///     Token for pagination
    /// </summary>
    public string? NextToken { get; set; }
}

/// <summary>
///     Claude tool definition for JSON request
/// </summary>
public class ClaudeTool
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("input_schema")]
    public object InputSchema { get; set; } = new();
}

/// <summary>
///     Claude tool choice configuration
/// </summary>
public class ClaudeToolChoice
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "auto"; // "auto", "any", "tool"

    [JsonPropertyName("name")]
    public string? Name { get; set; } // Required when type is "tool"
}

/// <summary>
///     Claude tool use result in message content
/// </summary>
public class ClaudeToolUse
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "tool_use";

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("input")]
    public object Input { get; set; } = new();
}

/// <summary>
///     Claude tool result in message content (for responses from tool calls)
/// </summary>
public class ClaudeToolResult
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "tool_result";

    [JsonPropertyName("tool_use_id")]
    public string ToolUseId { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("is_error")]
    public bool IsError { get; set; } = false;
}

/// <summary>
///     Helper class for converting between Bedrock ToolConfiguration and Claude tool formats
/// </summary>
public static class ToolConfigurationHelper
{
    /// <summary>
    ///     Converts a Bedrock ToolConfiguration to Claude tools format
    /// </summary>
    public static List<ClaudeTool>? ConvertToClaudeTools(ToolConfiguration? toolConfig)
    {
        if (toolConfig?.Tools == null || !toolConfig.Tools.Any())
            return null;

        var claudeTools = new List<ClaudeTool>();

        foreach (Tool? tool in toolConfig.Tools)
            if (tool.ToolSpec != null)
            {
                var claudeTool = new ClaudeTool
                {
                    Name = tool.ToolSpec.Name ?? string.Empty,
                    Description = tool.ToolSpec.Description ?? string.Empty,
                    InputSchema = ConvertInputSchema(tool.ToolSpec.InputSchema)
                };

                claudeTools.Add(claudeTool);
            }

        return claudeTools.Any() ? claudeTools : null;
    }

    /// <summary>
    ///     Converts Bedrock ToolInputSchema to Claude input schema format
    /// </summary>
    private static object ConvertInputSchema(ToolInputSchema? inputSchema)
    {
        if (inputSchema?.Json == null)
            // Return a basic schema if none provided
            return new
            {
                type = "object",
                properties = new { },
                required = new string[0]
            };

        try
        {
            // Try to parse as JSON and return as object
            string? jsonString = inputSchema.Json.AsString();
            return JsonSerializer.Deserialize<object>(jsonString) ?? new { };
        }
        catch
        {
            // If parsing fails, return basic schema
            return new
            {
                type = "object",
                properties = new { },
                required = new string[0]
            };
        }
    }
}

/// <summary>
///     Represents a simplified LLM processing record without document downloads
/// </summary>
public class LlmRecord
{
    /// <summary>
    ///     Patient ID for context (required)
    /// </summary>
    public required string PatientId { get; set; }

    /// <summary>
    ///     Practice/Organization ID for context (required)
    /// </summary>
    public required string PracticeId { get; set; }

    /// <summary>
    ///     Optional metadata as key-value pairs (format: "key1=value1;key2=value2")
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    ///     Sequence number for tracking
    /// </summary>
    public int SequenceNumber { get; set; }

    /// <summary>
    ///     Parse metadata string into dictionary
    /// </summary>
    public Dictionary<string, string> ParseMetadata()
    {
        if (string.IsNullOrWhiteSpace(Metadata))
            return new Dictionary<string, string>();

        return Metadata
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim());
    }

    /// <summary>
    ///     Returns a string representation of the record
    /// </summary>
    public override string ToString()
    {
        return $"Sequence {SequenceNumber}: Patient {PatientId}, Practice {PracticeId}" +
               (Metadata != null ? $" (Metadata: {Metadata})" : "");
    }
}