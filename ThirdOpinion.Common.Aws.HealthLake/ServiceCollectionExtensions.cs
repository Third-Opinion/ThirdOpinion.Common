using System.Linq;
using Amazon;
using Amazon.HealthLake;
using Amazon.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ThirdOpinion.Common.Aws.HealthLake.Aws;
using ThirdOpinion.Common.Aws.HealthLake.Configuration;
using ThirdOpinion.Common.Aws.HealthLake.Http;

namespace ThirdOpinion.Common.Aws.HealthLake;

/// <summary>
///     Extension methods for registering HealthLake services with dependency injection
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds HealthLake FHIR services to the service collection
    /// </summary>
    /// <param name="services">The service collection to add services to</param>
    /// <param name="configuration">Configuration containing HealthLake settings</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddHealthLakeServices(this IServiceCollection services,
        IConfiguration configuration)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));

        // Register configuration
        services.Configure<HealthLakeConfig>(configuration.GetSection("AWS:HealthLake"));

        // Register AWS credentials - only if not already registered
        // Uses default credential chain: env vars, profile, IAM role, etc.
        if (!services.Any(x => x.ServiceType == typeof(AWSCredentials)))
        {
            services.AddSingleton<AWSCredentials>(sp =>
            {
                // Try to get credentials from environment variables first
                string? accessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
                string? secretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");

                if (!string.IsNullOrEmpty(accessKey) && !string.IsNullOrEmpty(secretKey))
                {
                    return new EnvironmentVariablesAWSCredentials();
                }

                // If no environment variables, throw clear error with instructions
                throw new InvalidOperationException(
                    "AWS credentials not found. Please set AWS_ACCESS_KEY_ID and AWS_SECRET_ACCESS_KEY environment variables. " +
                    "For production environments, use IAM roles (EC2, ECS, Lambda, etc.).");
            });
        }

        // Register AWS signature service
        services.AddSingleton<IAwsSignatureService, AwsSignatureService>();

        // Register AWS HealthLake client
        services.AddSingleton<IAmazonHealthLake>(sp =>
        {
            HealthLakeConfig config = sp.GetRequiredService<IOptions<HealthLakeConfig>>().Value;
            var awsConfig = new AmazonHealthLakeConfig
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(config.Region)
            };
            return new AmazonHealthLakeClient(awsConfig);
        });

        // Register HTTP client for HealthLakeFhirService
        services.AddHttpClient<IHealthLakeHttpService, HealthLakeHttpService>(
            nameof(HealthLakeFhirService), (serviceProvider, client) =>
            {
                HealthLakeConfig config = serviceProvider
                    .GetRequiredService<IOptions<HealthLakeConfig>>().Value;
                client.Timeout = TimeSpan.FromSeconds(config.RequestTimeoutSeconds);
            });

        // Register HealthLake HTTP service
        services.AddScoped<IHealthLakeHttpService, HealthLakeHttpService>();

        // Register FHIR destination and source services
        services.AddScoped<IFhirDestinationService, HealthLakeFhirService>();
        services.AddScoped<IFhirSourceService>(sp => sp.GetRequiredService<IFhirDestinationService>() as IFhirSourceService ?? throw new InvalidOperationException("HealthLakeFhirService not registered"));
        services.AddScoped<HealthLakeFhirService>();

        return services;
    }
}