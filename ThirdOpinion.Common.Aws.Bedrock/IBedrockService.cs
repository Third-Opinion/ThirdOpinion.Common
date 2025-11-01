namespace ThirdOpinion.Common.Aws.Bedrock;

/// <summary>
///     Service for invoking AI models via AWS Bedrock runtime
/// </summary>
public interface IBedrockService
{
    /// <summary>
    ///     Invokes a model on AWS Bedrock with the provided prompt and parameters
    /// </summary>
    /// <param name="modelId">The identifier of the model to invoke (e.g., anthropic.claude-3-sonnet-20240229-v1:0)</param>
    /// <param name="prompt">The prompt to send to the model</param>
    /// <param name="parameters">Additional parameters for model invocation (temperature, max_tokens, etc.)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Model invocation response containing the generated content and metadata</returns>
    Task<ModelInvocationResponse> InvokeModelAsync(
        string modelId,
        string prompt,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Invokes a model with a structured request object
    /// </summary>
    /// <param name="request">The model invocation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <param name="promptName">Prompt name for tracing (required)</param>
    /// <param name="promptVersion">Prompt version for tracing (required)</param>
    /// <returns>Model invocation response</returns>
    Task<ModelInvocationResponse> InvokeModelAsync(
        ModelInvocationRequest request,
        string promptName,
        string promptVersion,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets the list of available models that can be invoked (Claude 4.x models only)
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of available model identifiers</returns>
    Task<IEnumerable<string>>
        GetAvailableModelsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets detailed information about available foundation models
    /// </summary>
    /// <param name="byProvider">Optional provider filter (e.g., "Anthropic", "Amazon", "Meta")</param>
    /// <param name="includeUnsupportedClaude3">Whether to include unsupported Claude 3.x models</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of foundation model details</returns>
    Task<IEnumerable<BedrockFoundationModel>> GetFoundationModelsAsync(
        string? byProvider = null,
        bool includeUnsupportedClaude3 = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets information about a specific model
    /// </summary>
    /// <param name="modelId">The model identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Model information if found, null otherwise</returns>
    Task<BedrockFoundationModel?> GetModelInfoAsync(string modelId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Tests connectivity to AWS Bedrock service
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if service is accessible, false otherwise</returns>
    Task<bool> TestConnectivityAsync(CancellationToken cancellationToken = default);
}