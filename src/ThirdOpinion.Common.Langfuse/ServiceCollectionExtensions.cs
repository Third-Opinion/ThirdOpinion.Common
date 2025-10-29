using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using FhirTools.Configuration;

namespace FhirTools.Langfuse;

/// <summary>
/// Extension methods for registering Langfuse services in the dependency injection container
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Langfuse services to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddLangfuseServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Register configuration
        services.Configure<LangfuseConfig>(configuration.GetSection("Langfuse"));

        // Register HTTP client for Langfuse service
        services.AddHttpClient<LangfuseService>(client =>
        {
            var langfuseConfig = configuration.GetSection("Langfuse").Get<LangfuseConfig>() ?? new LangfuseConfig();
            client.BaseAddress = new Uri(langfuseConfig.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(langfuseConfig.RequestTimeoutSeconds);

            // Add Basic Auth header if credentials are provided
            if (!string.IsNullOrEmpty(langfuseConfig.PublicKey) && !string.IsNullOrEmpty(langfuseConfig.SecretKey))
            {
                var credentials = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{langfuseConfig.PublicKey}:{langfuseConfig.SecretKey}"));
                client.DefaultRequestHeaders.Add("Authorization", $"Basic {credentials}");
            }
        });

        // Register Langfuse service
        services.AddSingleton<ILangfuseService, LangfuseService>();

        // Register Langfuse schema service with caching
        services.AddSingleton<ILangfuseSchemaService, LangfuseSchemaService>();

        // Add memory cache if not already added
        services.AddMemoryCache();

        return services;
    }
}