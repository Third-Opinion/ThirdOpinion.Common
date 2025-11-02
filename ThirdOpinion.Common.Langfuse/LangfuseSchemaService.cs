using Amazon.BedrockRuntime.Model;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ThirdOpinion.Common.Langfuse;

/// <summary>
/// Service for retrieving and caching Langfuse prompts with schema information
/// and building tool configurations for Bedrock
/// </summary>
public class LangfuseSchemaService : ILangfuseSchemaService
{
    private readonly ILangfuseService _langfuseService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<LangfuseSchemaService> _logger;

    public LangfuseSchemaService(
        ILangfuseService langfuseService,
        IMemoryCache cache,
        ILogger<LangfuseSchemaService> logger)
    {
        _langfuseService = langfuseService ?? throw new ArgumentNullException(nameof(langfuseService));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<LangfusePromptWithSchema?> GetPromptWithSchema(
        string promptName,
        int? version = null,
        TimeSpan? cacheDuration = null)
    {
        if (string.IsNullOrWhiteSpace(promptName))
        {
            throw new ArgumentException("Prompt name cannot be null or empty", nameof(promptName));
        }

        var cacheKey = version.HasValue
            ? $"langfuse:prompt:{promptName}:v{version}"
            : $"langfuse:prompt:{promptName}:latest";

        // Try to get from cache
        if (_cache.TryGetValue(cacheKey, out LangfusePromptWithSchema? cachedPrompt))
        {
            _logger.LogDebug("Cache hit for {CacheKey}", cacheKey);
            return cachedPrompt;
        }

        _logger.LogDebug("Cache miss for {CacheKey}, fetching from Langfuse", cacheKey);

        try
        {
            // Fetch from Langfuse
            var versionString = version?.ToString();
            var prompt = await _langfuseService.GetPromptAsync(promptName, versionString);

            if (prompt == null)
            {
                _logger.LogWarning("Prompt '{PromptName}' version '{Version}' not found in Langfuse",
                    promptName, version?.ToString() ?? "latest");
                return null;
            }

            // Convert to LangfusePromptWithSchema
            var promptWithSchema = LangfuseSchemaHelper.ConvertToPromptWithSchema(prompt);

            // Cache it
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = cacheDuration ?? TimeSpan.FromHours(1),
                SlidingExpiration = TimeSpan.FromMinutes(15)
            };

            _cache.Set(cacheKey, promptWithSchema, cacheOptions);

            _logger.LogDebug("Successfully cached prompt with schema for {CacheKey}. Has schema: {HasSchema}",
                cacheKey, promptWithSchema.Schema != null);

            return promptWithSchema;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving prompt with schema for {PromptName} version {Version}",
                promptName, version?.ToString() ?? "latest");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<ToolConfiguration?> GetToolConfiguration(
        string promptName,
        int? version = null)
    {
        if (string.IsNullOrWhiteSpace(promptName))
        {
            throw new ArgumentException("Prompt name cannot be null or empty", nameof(promptName));
        }

        var cacheKey = version.HasValue
            ? $"langfuse:toolconfig:{promptName}:v{version}"
            : $"langfuse:toolconfig:{promptName}:latest";

        if (_cache.TryGetValue(cacheKey, out ToolConfiguration? cachedConfig))
        {
            _logger.LogDebug("Cache hit for tool config {CacheKey}", cacheKey);
            return cachedConfig;
        }

        _logger.LogDebug("Building tool config for {PromptName} version {Version}",
            promptName, version?.ToString() ?? "latest");

        try
        {
            var prompt = await GetPromptWithSchema(promptName, version);
            if (prompt?.Schema == null)
            {
                _logger.LogDebug("No schema available for prompt {PromptName}, no tool configuration created",
                    promptName);
                return null;
            }

            var toolConfig = prompt.BuildToolConfiguration();

            if (toolConfig != null)
            {
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1),
                    SlidingExpiration = TimeSpan.FromMinutes(15)
                };

                _cache.Set(cacheKey, toolConfig, cacheOptions);

                _logger.LogInformation("Successfully built and cached tool configuration for prompt {PromptName}. Tool name: {ToolName}",
                    promptName, toolConfig.Tools?.FirstOrDefault()?.ToolSpec?.Name ?? "unknown");
            }
            else
            {
                _logger.LogWarning("Failed to build tool configuration from schema for prompt {PromptName}", promptName);
            }

            return toolConfig;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building tool configuration for prompt {PromptName} version {Version}",
                promptName, version?.ToString() ?? "latest");
            return null;
        }
    }

    /// <inheritdoc />
    public void InvalidateCache(string promptName, int? version = null)
    {
        if (string.IsNullOrWhiteSpace(promptName))
        {
            throw new ArgumentException("Prompt name cannot be null or empty", nameof(promptName));
        }

        if (version.HasValue)
        {
            // Invalidate specific version
            var promptCacheKey = $"langfuse:prompt:{promptName}:v{version}";
            var toolConfigCacheKey = $"langfuse:toolconfig:{promptName}:v{version}";

            _cache.Remove(promptCacheKey);
            _cache.Remove(toolConfigCacheKey);

            _logger.LogInformation("Invalidated cache for {PromptName} version {Version}", promptName, version);
        }
        else
        {
            // Invalidate latest version
            var promptCacheKey = $"langfuse:prompt:{promptName}:latest";
            var toolConfigCacheKey = $"langfuse:toolconfig:{promptName}:latest";

            _cache.Remove(promptCacheKey);
            _cache.Remove(toolConfigCacheKey);

            _logger.LogInformation("Invalidated latest cache for {PromptName}", promptName);
        }
    }

    /// <inheritdoc />
    public void ClearCache()
    {
        // Memory cache doesn't have a clear all method, but we can implement
        // by tracking cache keys if needed. For now, log that clear was requested.
        _logger.LogInformation("Cache clear requested - cache entries will expire naturally");

        // If we need to implement full cache clearing, we could maintain a list of cache keys
        // or use a different caching mechanism. For now, relying on natural expiration.
    }
}
