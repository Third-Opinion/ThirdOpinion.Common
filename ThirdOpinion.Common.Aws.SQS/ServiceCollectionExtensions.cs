using Amazon;
using Amazon.SQS;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
        services.Configure<SqsOptions>(configuration.GetSection("SQS"));

        services.AddSingleton<IAmazonSQS>(sp =>
        {
            SqsOptions options = sp.GetRequiredService<IOptions<SqsOptions>>().Value;
            var config = new AmazonSQSConfig();

            if (!string.IsNullOrEmpty(options.ServiceUrl)) config.ServiceURL = options.ServiceUrl;

            if (!string.IsNullOrEmpty(options.Region))
                config.RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region);

            return new AmazonSQSClient(config);
        });

        services.AddSingleton<ISqsMessageQueue, SqsMessageQueue>();

        return services;
    }

    /// <summary>
    ///     Add SQS messaging with custom configuration
    /// </summary>
    public static IServiceCollection AddSqsMessaging(this IServiceCollection services,
        Action<SqsOptions> configureOptions)
    {
        services.Configure(configureOptions);

        services.AddSingleton<IAmazonSQS>(sp =>
        {
            SqsOptions options = sp.GetRequiredService<IOptions<SqsOptions>>().Value;
            var config = new AmazonSQSConfig();

            if (!string.IsNullOrEmpty(options.ServiceUrl)) config.ServiceURL = options.ServiceUrl;

            if (!string.IsNullOrEmpty(options.Region))
                config.RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region);

            return new AmazonSQSClient(config);
        });

        services.AddSingleton<ISqsMessageQueue, SqsMessageQueue>();

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