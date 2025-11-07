using ThirdOpinion.Common.Langfuse;

namespace ThirdOpinion.Common.Aws.Bedrock;

/// <summary>
///     Service for managing Bedrock model pricing and cost calculations
/// </summary>
public interface IBedrockPricingService
{
    /// <summary>
    ///     Calculates the cost for a Bedrock model invocation
    /// </summary>
    /// <param name="modelId">Bedrock model identifier</param>
    /// <param name="inputTokens">Number of input tokens</param>
    /// <param name="outputTokens">Number of output tokens</param>
    /// <param name="region">AWS region (optional, defaults to us-east-2)</param>
    /// <returns>Cost calculation result</returns>
    BedrockCostCalculation CalculateCost(string modelId,
        int inputTokens,
        int outputTokens,
        string? region = null);

    /// <summary>
    ///     Gets pricing information for a specific model
    /// </summary>
    /// <param name="modelId">Bedrock model identifier</param>
    /// <param name="region">AWS region (optional)</param>
    /// <returns>Pricing information if available</returns>
    BedrockModelPricing? GetModelPricing(string modelId, string? region = null);

    /// <summary>
    ///     Gets all available model pricing information
    /// </summary>
    /// <returns>Dictionary of model pricing keyed by model ID</returns>
    Dictionary<string, BedrockModelPricing> GetAllModelPricing();
}

/// <summary>
///     Implementation of Bedrock pricing service with current AWS pricing
/// </summary>
public class BedrockPricingService : IBedrockPricingService
{
    private static readonly Dictionary<string, BedrockModelPricing> ModelPricing = new()
    {
        // Anthropic Claude 4.x Models (as of January 2025)
        ["anthropic.claude-sonnet-4-5-20250929-v1:0"] = new BedrockModelPricing
        {
            ModelId = "anthropic.claude-sonnet-4-5-20250929-v1:0",
            ModelName = "Claude 4.5 Sonnet",
            Provider = "Anthropic",
            InputTokensPer1K = 0.003m,
            OutputTokensPer1K = 0.015m,
            Region = "us-east-2"
        },
        ["us.anthropic.claude-sonnet-4-5-20250929-v1:0"] = new BedrockModelPricing
        {
            ModelId = "us.anthropic.claude-sonnet-4-5-20250929-v1:0",
            ModelName = "Claude 4.5 Sonnet (US Profile)",
            Provider = "Anthropic",
            InputTokensPer1K = 0.003m,
            OutputTokensPer1K = 0.015m,
            Region = "us-east-2"
        },
        ["anthropic.claude-haiku-4-5-20250929-v1:0"] = new BedrockModelPricing
        {
            ModelId = "anthropic.claude-haiku-4-5-20250929-v1:0",
            ModelName = "Claude 4.5 Haiku",
            Provider = "Anthropic",
            InputTokensPer1K = 0.00025m,
            OutputTokensPer1K = 0.00125m,
            Region = "us-east-2"
        },
        ["us.anthropic.claude-haiku-4-5-20250929-v1:0"] = new BedrockModelPricing
        {
            ModelId = "us.anthropic.claude-haiku-4-5-20250929-v1:0",
            ModelName = "Claude 4.5 Haiku (US Profile)",
            Provider = "Anthropic",
            InputTokensPer1K = 0.00025m,
            OutputTokensPer1K = 0.00125m,
            Region = "us-east-2"
        },
        ["anthropic.claude-opus-4-1-20250805-v1:0"] = new BedrockModelPricing
        {
            ModelId = "anthropic.claude-opus-4-1-20250805-v1:0",
            ModelName = "Claude 4.1 Opus",
            Provider = "Anthropic",
            InputTokensPer1K = 0.015m,
            OutputTokensPer1K = 0.075m,
            Region = "us-east-2"
        },
        ["us.anthropic.claude-opus-4-1-20250805-v1:0"] = new BedrockModelPricing
        {
            ModelId = "us.anthropic.claude-opus-4-1-20250805-v1:0",
            ModelName = "Claude 4.1 Opus (US Profile)",
            Provider = "Anthropic",
            InputTokensPer1K = 0.015m,
            OutputTokensPer1K = 0.075m,
            Region = "us-east-2"
        },

        // Anthropic Claude 3.x Models (Legacy)
        ["anthropic.claude-3-5-sonnet-20240620-v1:0"] = new BedrockModelPricing
        {
            ModelId = "anthropic.claude-3-5-sonnet-20240620-v1:0",
            ModelName = "Claude 3.5 Sonnet",
            Provider = "Anthropic",
            InputTokensPer1K = 0.003m,
            OutputTokensPer1K = 0.015m,
            Region = "us-east-2"
        },
        ["us.anthropic.claude-3-5-sonnet-20241022-v2:0"] = new BedrockModelPricing
        {
            ModelId = "us.anthropic.claude-3-5-sonnet-20241022-v2:0",
            ModelName = "Claude 3.5 Sonnet v2 (US Profile)",
            Provider = "Anthropic",
            InputTokensPer1K = 0.003m,
            OutputTokensPer1K = 0.015m,
            Region = "us-east-2"
        },
        ["anthropic.claude-3-opus-20240229-v1:0"] = new BedrockModelPricing
        {
            ModelId = "anthropic.claude-3-opus-20240229-v1:0",
            ModelName = "Claude 3 Opus",
            Provider = "Anthropic",
            InputTokensPer1K = 0.015m,
            OutputTokensPer1K = 0.075m,
            Region = "us-east-2"
        },
        ["us.anthropic.claude-3-opus-20240229-v1:0"] = new BedrockModelPricing
        {
            ModelId = "us.anthropic.claude-3-opus-20240229-v1:0",
            ModelName = "Claude 3 Opus (US Profile)",
            Provider = "Anthropic",
            InputTokensPer1K = 0.015m,
            OutputTokensPer1K = 0.075m,
            Region = "us-east-2"
        },
        ["anthropic.claude-3-haiku-20240307-v1:0"] = new BedrockModelPricing
        {
            ModelId = "anthropic.claude-3-haiku-20240307-v1:0",
            ModelName = "Claude 3 Haiku",
            Provider = "Anthropic",
            InputTokensPer1K = 0.00025m,
            OutputTokensPer1K = 0.00125m,
            Region = "us-east-2"
        },
        ["us.anthropic.claude-3-haiku-20240307-v1:0"] = new BedrockModelPricing
        {
            ModelId = "us.anthropic.claude-3-haiku-20240307-v1:0",
            ModelName = "Claude 3 Haiku (US Profile)",
            Provider = "Anthropic",
            InputTokensPer1K = 0.00025m,
            OutputTokensPer1K = 0.00125m,
            Region = "us-east-2"
        },

        // Amazon Titan Models
        ["amazon.titan-text-express-v1"] = new BedrockModelPricing
        {
            ModelId = "amazon.titan-text-express-v1",
            ModelName = "Titan Text Express",
            Provider = "Amazon",
            InputTokensPer1K = 0.0002m,
            OutputTokensPer1K = 0.0006m,
            Region = "us-east-2"
        },
        ["amazon.titan-text-lite-v1"] = new BedrockModelPricing
        {
            ModelId = "amazon.titan-text-lite-v1",
            ModelName = "Titan Text Lite",
            Provider = "Amazon",
            InputTokensPer1K = 0.00015m,
            OutputTokensPer1K = 0.0002m,
            Region = "us-east-2"
        },
        ["amazon.titan-embed-text-v1"] = new BedrockModelPricing
        {
            ModelId = "amazon.titan-embed-text-v1",
            ModelName = "Titan Embeddings",
            Provider = "Amazon",
            InputTokensPer1K = 0.0001m,
            OutputTokensPer1K = 0.0m, // Embeddings don't have output tokens
            Region = "us-east-2"
        },

        // Amazon Nova Models (as of December 2024)
        ["amazon.nova-pro-v1:0"] = new BedrockModelPricing
        {
            ModelId = "amazon.nova-pro-v1:0",
            ModelName = "Nova Pro",
            Provider = "Amazon",
            InputTokensPer1K = 0.0008m,
            OutputTokensPer1K = 0.0032m,
            Region = "us-east-2"
        },
        ["amazon.nova-lite-v1:0"] = new BedrockModelPricing
        {
            ModelId = "amazon.nova-lite-v1:0",
            ModelName = "Nova Lite",
            Provider = "Amazon",
            InputTokensPer1K = 0.00006m,
            OutputTokensPer1K = 0.00024m,
            Region = "us-east-2"
        },
        ["amazon.nova-micro-v1:0"] = new BedrockModelPricing
        {
            ModelId = "amazon.nova-micro-v1:0",
            ModelName = "Nova Micro",
            Provider = "Amazon",
            InputTokensPer1K = 0.000035m,
            OutputTokensPer1K = 0.00014m,
            Region = "us-east-2"
        },
        ["amazon.nova-premier-v1:0"] = new BedrockModelPricing
        {
            ModelId = "amazon.nova-premier-v1:0",
            ModelName = "Nova Premier",
            Provider = "Amazon",
            InputTokensPer1K = 0.003m,
            OutputTokensPer1K = 0.012m,
            Region = "us-east-2"
        },

        // Meta Llama Models
        ["meta.llama2-7b-chat-v1"] = new BedrockModelPricing
        {
            ModelId = "meta.llama2-7b-chat-v1",
            ModelName = "Llama 2 7B Chat",
            Provider = "Meta",
            InputTokensPer1K = 0.00065m,
            OutputTokensPer1K = 0.00065m,
            Region = "us-east-2"
        },
        ["meta.llama2-13b-chat-v1"] = new BedrockModelPricing
        {
            ModelId = "meta.llama2-13b-chat-v1",
            ModelName = "Llama 2 13B Chat",
            Provider = "Meta",
            InputTokensPer1K = 0.00075m,
            OutputTokensPer1K = 0.001m,
            Region = "us-east-2"
        },
        ["meta.llama2-70b-chat-v1"] = new BedrockModelPricing
        {
            ModelId = "meta.llama2-70b-chat-v1",
            ModelName = "Llama 2 70B Chat",
            Provider = "Meta",
            InputTokensPer1K = 0.00195m,
            OutputTokensPer1K = 0.00256m,
            Region = "us-east-2"
        },
        ["meta.llama3-2-3b-instruct-v1:0"] = new BedrockModelPricing
        {
            ModelId = "meta.llama3-2-3b-instruct-v1:0",
            ModelName = "Llama 3.2 3B Instruct",
            Provider = "Meta",
            InputTokensPer1K = 0.0005m,
            OutputTokensPer1K = 0.0005m,
            Region = "us-east-2"
        },
        ["us.meta.llama3-2-3b-instruct-v1:0"] = new BedrockModelPricing
        {
            ModelId = "us.meta.llama3-2-3b-instruct-v1:0",
            ModelName = "Llama 3.2 3B Instruct (US Profile)",
            Provider = "Meta",
            InputTokensPer1K = 0.0005m,
            OutputTokensPer1K = 0.0005m,
            Region = "us-east-2"
        }
    };

    public BedrockCostCalculation CalculateCost(string modelId,
        int inputTokens,
        int outputTokens,
        string? region = null)
    {
        BedrockModelPricing? pricing = GetModelPricing(modelId, region);

        if (pricing == null)
            // Return zero cost for unknown models
            return new BedrockCostCalculation
            {
                InputCost = 0m,
                OutputCost = 0m,
                TotalCost = 0m,
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                TotalTokens = inputTokens + outputTokens,
                ModelId = modelId,
                Currency = "USD"
            };

        decimal inputCost = inputTokens / 1000m * pricing.InputTokensPer1K;
        decimal outputCost = outputTokens / 1000m * pricing.OutputTokensPer1K;
        decimal totalCost = inputCost + outputCost;

        return new BedrockCostCalculation
        {
            InputCost = Math.Round(inputCost, 6),
            OutputCost = Math.Round(outputCost, 6),
            TotalCost = Math.Round(totalCost, 6),
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            TotalTokens = inputTokens + outputTokens,
            ModelId = modelId,
            Currency = "USD"
        };
    }

    public BedrockModelPricing? GetModelPricing(string modelId, string? region = null)
    {
        if (string.IsNullOrWhiteSpace(modelId))
            return null;

        // First try exact match
        if (ModelPricing.TryGetValue(modelId, out BedrockModelPricing? pricing))
            return pricing;

        // Try to normalize inference profile models
        string normalizedModelId = NormalizeModelId(modelId);
        if (normalizedModelId != modelId && ModelPricing.TryGetValue(normalizedModelId,
                out BedrockModelPricing? normalizedPricing))
            return normalizedPricing;

        // Try to find base model without region prefix
        string baseModelId = RemoveRegionPrefix(modelId);
        if (baseModelId != modelId &&
            ModelPricing.TryGetValue(baseModelId, out BedrockModelPricing? basePricing))
            return basePricing;

        return null;
    }

    public Dictionary<string, BedrockModelPricing> GetAllModelPricing()
    {
        return new Dictionary<string, BedrockModelPricing>(ModelPricing);
    }

    /// <summary>
    ///     Removes region prefixes (us., eu., etc.) from model IDs
    /// </summary>
    private static string RemoveRegionPrefix(string modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
            return modelId;

        var prefixes = new[] { "us.", "eu.", "ap.", "ca." };
        foreach (string prefix in prefixes)
            if (modelId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return modelId.Substring(prefix.Length);

        return modelId;
    }

    /// <summary>
    ///     Normalizes model IDs by handling different naming patterns
    /// </summary>
    private static string NormalizeModelId(string modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
            return modelId;

        // Handle common normalization patterns
        string normalized = modelId.ToLowerInvariant().Trim();

        // Map inference profile IDs to base model IDs
        var mappings = new Dictionary<string, string>
        {
            // Claude 4.x mappings
            ["us.anthropic.claude-sonnet-4-5-20250929-v1:0"]
                = "anthropic.claude-sonnet-4-5-20250929-v1:0",
            ["eu.anthropic.claude-sonnet-4-5-20250929-v1:0"]
                = "anthropic.claude-sonnet-4-5-20250929-v1:0",

            // Claude 3.x mappings
            ["us.anthropic.claude-3-5-sonnet-20241022-v2:0"]
                = "anthropic.claude-3-5-sonnet-20240620-v1:0",
            ["us.anthropic.claude-3-haiku-20240307-v1:0"]
                = "anthropic.claude-3-haiku-20240307-v1:0",
            ["us.anthropic.claude-3-opus-20240229-v1:0"] = "anthropic.claude-3-opus-20240229-v1:0",

            // Llama mappings
            ["us.meta.llama3-2-3b-instruct-v1:0"] = "meta.llama3-2-3b-instruct-v1:0"
        };

        if (mappings.TryGetValue(normalized, out string? mappedId))
            return mappedId;

        return modelId;
    }
}