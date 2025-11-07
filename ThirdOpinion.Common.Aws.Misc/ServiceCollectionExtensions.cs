using Amazon;
using Amazon.SecretsManager;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ThirdOpinion.Common.Aws.Misc.Configuration;
using ThirdOpinion.Common.Aws.Misc.SecretsManager;

namespace ThirdOpinion.Common.Aws.Misc;

/// <summary>
///     Extension methods for configuring AWS miscellaneous services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Add AWS miscellaneous services including Secrets Manager and signature services
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">Application configuration</param>
    /// <returns>The modified service collection</returns>
    public static IServiceCollection AddAwsMiscServices(this IServiceCollection services,
        IConfiguration configuration)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));

        // Register Secrets Manager configuration
        services.Configure<SecretsManagerConfig>(configuration.GetSection("AWS:SecretsManager"));

        // Register Secrets Manager client
        services.AddSingleton<IAmazonSecretsManager>(sp =>
        {
            var config = configuration.GetSection("AWS:SecretsManager").Get<SecretsManagerConfig>();
            if (config == null || string.IsNullOrEmpty(config.Region))
            {
                // Use default region from environment or us-east-2
                string region = Environment.GetEnvironmentVariable("AWS_REGION")
                                ?? Environment.GetEnvironmentVariable("AWS_DEFAULT_REGION")
                                ?? "us-east-2";
                return new AmazonSecretsManagerClient(RegionEndpoint.GetBySystemName(region));
            }

            // Use configured region
            return new AmazonSecretsManagerClient(RegionEndpoint.GetBySystemName(config.Region));
        });

        // Register Secrets Manager service
        services.AddSingleton<ISecretsManagerService, SecretsManagerService>();

        // Register AWS signature service
        services.AddSingleton<IAwsSignatureService, AwsSignatureService>();

        // Register memory cache for secrets caching
        services.AddMemoryCache();

        return services;
    }

    /// <summary>
    ///     Add AWS miscellaneous services with custom configuration
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Action to configure options</param>
    /// <returns>The modified service collection</returns>
    public static IServiceCollection AddAwsMiscServices(
        this IServiceCollection services,
        Action<AwsMiscOptions> configureOptions)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));
        if (configureOptions == null) throw new ArgumentNullException(nameof(configureOptions));

        var options = new AwsMiscOptions();
        configureOptions(options);

        // Configure Secrets Manager
        if (options.SecretsManagerConfig != null)
            services.Configure<SecretsManagerConfig>(config =>
            {
                config.Region = options.SecretsManagerConfig.Region;
                config.SecretName = options.SecretsManagerConfig.SecretName;
                config.CacheTtlMinutes = options.SecretsManagerConfig.CacheTtlMinutes;
                config.EnableCaching = options.SecretsManagerConfig.EnableCaching;
            });

        // Register Secrets Manager client
        services.AddSingleton<IAmazonSecretsManager>(sp =>
        {
            string region = options.SecretsManagerConfig?.Region
                            ?? Environment.GetEnvironmentVariable("AWS_REGION")
                            ?? Environment.GetEnvironmentVariable("AWS_DEFAULT_REGION")
                            ?? "us-east-2";

            return new AmazonSecretsManagerClient(RegionEndpoint.GetBySystemName(region));
        });

        // Register services
        services.AddSingleton<ISecretsManagerService, SecretsManagerService>();
        services.AddSingleton<IAwsSignatureService, AwsSignatureService>();
        services.AddMemoryCache();

        return services;
    }
}

/// <summary>
///     Options for configuring AWS miscellaneous services
/// </summary>
public class AwsMiscOptions
{
    /// <summary>
    ///     Configuration for AWS Secrets Manager
    /// </summary>
    public SecretsManagerConfig? SecretsManagerConfig { get; set; }

    /// <summary>
    ///     AWS region to use for services
    /// </summary>
    public string? Region { get; set; }
}