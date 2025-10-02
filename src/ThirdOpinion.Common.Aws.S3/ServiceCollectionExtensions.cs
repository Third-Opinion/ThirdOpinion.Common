using Amazon;
using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ThirdOpinion.Common.Aws.S3;

/// <summary>
///     Extension methods for registering S3 services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Add S3 storage to the service collection
    /// </summary>
    public static IServiceCollection AddS3Storage(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<S3Options>(configuration.GetSection("S3"));

        services.AddSingleton<IAmazonS3>(sp =>
        {
            S3Options options = sp.GetRequiredService<IOptions<S3Options>>().Value;
            var config = new AmazonS3Config();

            if (!string.IsNullOrEmpty(options.ServiceUrl))
            {
                config.ServiceURL = options.ServiceUrl;
                config.ForcePathStyle = true; // Required for LocalStack
            }

            if (!string.IsNullOrEmpty(options.Region))
                config.RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region);

            return new AmazonS3Client(config);
        });

        services.AddSingleton<IS3Storage, S3Storage>();

        return services;
    }

    /// <summary>
    ///     Add S3 storage with custom configuration
    /// </summary>
    public static IServiceCollection AddS3Storage(this IServiceCollection services,
        Action<S3Options> configureOptions)
    {
        services.Configure(configureOptions);

        services.AddSingleton<IAmazonS3>(sp =>
        {
            S3Options options = sp.GetRequiredService<IOptions<S3Options>>().Value;
            var config = new AmazonS3Config();

            if (!string.IsNullOrEmpty(options.ServiceUrl))
            {
                config.ServiceURL = options.ServiceUrl;
                config.ForcePathStyle = true; // Required for LocalStack
            }

            if (!string.IsNullOrEmpty(options.Region))
                config.RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region);

            return new AmazonS3Client(config);
        });

        services.AddSingleton<IS3Storage, S3Storage>();

        return services;
    }
}

/// <summary>
///     Options for configuring S3
/// </summary>
public class S3Options
{
    /// <summary>
    ///     S3 service URL (for local development)
    /// </summary>
    public string? ServiceUrl { get; set; }

    /// <summary>
    ///     AWS region
    /// </summary>
    public string? Region { get; set; }
}