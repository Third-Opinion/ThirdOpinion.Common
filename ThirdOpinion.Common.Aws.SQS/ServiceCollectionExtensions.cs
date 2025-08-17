using Amazon;
using Amazon.SQS;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Linq;

namespace ThirdOpinion.Common.Aws.SQS;

/// <summary>
///     Extension methods for registering SQS services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Add SQS messaging to the service collection
    /// </summary>
    public static IServiceCollection AddSqsMessaging(this IServiceCollection services,
        IConfiguration configuration)
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));

        services.Configure<SqsOptions>(configuration.GetSection("SQS"));

        // Only register if not already registered
        if (!services.Any(x => x.ServiceType == typeof(IAmazonSQS)))
        {
            services.AddSingleton<IAmazonSQS>(sp =>
            {
                SqsOptions options = sp.GetRequiredService<IOptions<SqsOptions>>().Value;
                var config = new AmazonSQSConfig();

                if (!string.IsNullOrEmpty(options.ServiceUrl))
                {
                    config.ServiceURL = options.ServiceUrl;
                }
                else if (!string.IsNullOrEmpty(options.Region))
                {
                    config.RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region);
                }
                else
                {
                    // Default to us-east-1 if no configuration is provided
                    config.RegionEndpoint = RegionEndpoint.USEast1;
                }

                return new AmazonSQSClient(config);
            });
        }

        // Only register if not already registered
        if (!services.Any(x => x.ServiceType == typeof(ISqsMessageQueue)))
        {
            services.AddSingleton<ISqsMessageQueue, SqsMessageQueue>();
        }

        return services;
    }

    /// <summary>
    ///     Add SQS messaging with custom configuration
    /// </summary>
    public static IServiceCollection AddSqsMessaging(this IServiceCollection services,
        Action<SqsOptions> configureOptions)
    {
        if (configureOptions == null)
            throw new ArgumentNullException(nameof(configureOptions));

        services.Configure(configureOptions);

        // Only register if not already registered
        if (!services.Any(x => x.ServiceType == typeof(IAmazonSQS)))
        {
            services.AddSingleton<IAmazonSQS>(sp =>
            {
                SqsOptions options = sp.GetRequiredService<IOptions<SqsOptions>>().Value;
                var config = new AmazonSQSConfig();

                if (!string.IsNullOrEmpty(options.ServiceUrl))
                {
                    config.ServiceURL = options.ServiceUrl;
                }
                else if (!string.IsNullOrEmpty(options.Region))
                {
                    config.RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region);
                }
                else
                {
                    // Default to us-east-1 if no configuration is provided
                    config.RegionEndpoint = RegionEndpoint.USEast1;
                }

                return new AmazonSQSClient(config);
            });
        }

        // Only register if not already registered
        if (!services.Any(x => x.ServiceType == typeof(ISqsMessageQueue)))
        {
            services.AddSingleton<ISqsMessageQueue, SqsMessageQueue>();
        }

        return services;
    }
}

/// <summary>
///     Options for configuring SQS
/// </summary>
public class SqsOptions
{
    /// <summary>
    ///     SQS service URL (for local development)
    /// </summary>
    public string? ServiceUrl { get; set; }

    /// <summary>
    ///     AWS region
    /// </summary>
    public string? Region { get; set; }
}