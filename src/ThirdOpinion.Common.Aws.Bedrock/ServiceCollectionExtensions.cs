using Amazon.Bedrock;
using Amazon.BedrockRuntime;
using ThirdOpinion.Common.Aws.Bedrock.Configuration;
using ThirdOpinion.Common.Misc.RateLimiting;
using ThirdOpinion.Common.Misc.Retry;
using ThirdOpinion.Common.Logging;
using ThirdOpinion.Common.Langfuse;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ThirdOpinion.Common.Aws.Bedrock;

/// <summary>
/// Extension methods for registering Bedrock services in the dependency injection container
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Bedrock services to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddBedrockServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register configuration
        services.Configure<BedrockConfig>(configuration.GetSection("Bedrock"));

        // Register AWS Bedrock clients
        services.AddSingleton(typeof(IAmazonBedrockRuntime), typeof(AmazonBedrockRuntimeClient));
        services.AddSingleton(typeof(IAmazonBedrock), typeof(AmazonBedrockClient));

        // Register Bedrock pricing service
        services.AddSingleton<IBedrockPricingService, BedrockPricingService>();

        // Configure HTTP client for Bedrock service
        services.AddHttpClient<BedrockService>(client =>
        {
            var bedrockConfig = configuration.GetSection("Bedrock").Get<BedrockConfig>() ?? new BedrockConfig();
            client.Timeout = TimeSpan.FromSeconds(bedrockConfig.TimeoutSeconds);
        });

        // Register Bedrock service
        services.AddSingleton<IBedrockService>(serviceProvider =>
        {
            var bedrockRuntimeClient = serviceProvider.GetRequiredService(typeof(IAmazonBedrockRuntime)) as IAmazonBedrockRuntime;
            var logger = serviceProvider.GetRequiredService<ILogger<BedrockService>>();
            var correlationIdProvider = serviceProvider.GetService<ICorrelationIdProvider>();

            // Optional services for tracing
            var langfuseService = serviceProvider.GetService<ILangfuseService>();
            var pricingService = serviceProvider.GetService<IBedrockPricingService>() ?? new BedrockPricingService();

            return new BedrockService(
                bedrockRuntimeClient!,
                logger,
                correlationIdProvider,
                langfuseService,
                pricingService);
        });

        // Register rate limiting for Bedrock - this will be handled in the main rate limiter configuration

        return services;
    }
}