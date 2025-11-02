using Amazon.BedrockRuntime.Model;

namespace ThirdOpinion.Common.Langfuse;

/// <summary>
/// Service for retrieving and caching Langfuse prompts with schema information
/// and building tool configurations for Bedrock
/// </summary>
public interface ILangfuseSchemaService
{
    /// <summary>
    /// Retrieves a prompt with parsed schema information, with caching
    /// </summary>
    /// <param name="promptName">Name of the prompt to retrieve</param>
    /// <param name="version">Optional version of the prompt (defaults to latest)</param>
    /// <param name="cacheDuration">Optional cache duration override</param>
    /// <returns>Prompt with schema information</returns>
    Task<LangfusePromptWithSchema?> GetPromptWithSchema(
        string promptName,
        int? version = null,
        TimeSpan? cacheDuration = null);

    /// <summary>
    /// Retrieves a pre-built and cached ToolConfiguration for a prompt
    /// </summary>
    /// <param name="promptName">Name of the prompt</param>
    /// <param name="version">Optional version of the prompt (defaults to latest)</param>
    /// <returns>ToolConfiguration for Bedrock, or null if no schema available</returns>
    Task<ToolConfiguration?> GetToolConfiguration(
        string promptName,
        int? version = null);

    /// <summary>
    /// Manually invalidates cache for a specific prompt
    /// </summary>
    /// <param name="promptName">Name of the prompt to invalidate</param>
    /// <param name="version">Optional version to invalidate (defaults to all versions)</param>
    void InvalidateCache(string promptName, int? version = null);

    /// <summary>
    /// Clears all cached prompts and tool configurations
    /// </summary>
    void ClearCache();
}
