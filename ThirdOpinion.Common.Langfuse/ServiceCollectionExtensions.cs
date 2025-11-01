using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ThirdOpinion.Common.Langfuse.Configuration;

namespace ThirdOpinion.Common.Langfuse;

/// <summary>
///     Extension methods for registering Langfuse services in the dependency injection container
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds Langfuse services to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddLangfuseServices(this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register configuration
        services.Configure<LangfuseConfiguration>(configuration.GetSection("Langfuse"));

        // Register HTTP client for Langfuse service
        services.AddHttpClient<LangfuseService>(client =>
        {
            LangfuseConfiguration langfuseConfig
                = configuration.GetSection("Langfuse").Get<LangfuseConfiguration>() ??
                  new LangfuseConfiguration();
            client.BaseAddress = new Uri(langfuseConfig.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(langfuseConfig.RequestTimeoutSeconds);

            // Add Basic Auth header if credentials are provided
            if (!string.IsNullOrEmpty(langfuseConfig.PublicKey) &&
                !string.IsNullOrEmpty(langfuseConfig.SecretKey))
            {
                string credentials = Convert.ToBase64String(
                    Encoding.ASCII.GetBytes(
                        $"{langfuseConfig.PublicKey}:{langfuseConfig.SecretKey}"));
                client.DefaultRequestHeaders.Add("Authorization", $"Basic {credentials}");
            }
        });

        // Register Langfuse service
        services.AddSingleton<ILangfuseService, LangfuseService>();

        // Add memory cache if not already added
        services.AddMemoryCache();

        return services;
    }
}