using Amazon;
using Amazon.DynamoDBv2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ThirdOpinion.Common.Aws.DynamoDb;

/// <summary>
///     Extension methods for registering DynamoDB services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Add DynamoDB repository to the service collection
    /// </summary>
    public static IServiceCollection AddDynamoDbRepository(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<DynamoDbOptions>(configuration.GetSection("DynamoDb"));

        services.AddSingleton<IAmazonDynamoDB>(sp =>
        {
            DynamoDbOptions options = sp.GetRequiredService<IOptions<DynamoDbOptions>>().Value;
            var config = new AmazonDynamoDBConfig();

            if (!string.IsNullOrEmpty(options.ServiceUrl)) config.ServiceURL = options.ServiceUrl;

            if (!string.IsNullOrEmpty(options.Region))
                config.RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region);

            return new AmazonDynamoDBClient(config);
        });

        services.AddSingleton<IDynamoDbRepository, DynamoDbRepository>();

        return services;
    }

    /// <summary>
    ///     Add DynamoDB repository with custom configuration
    /// </summary>
    public static IServiceCollection AddDynamoDbRepository(this IServiceCollection services,
        Action<DynamoDbOptions> configureOptions)
    {
        services.Configure(configureOptions);

        services.AddSingleton<IAmazonDynamoDB>(sp =>
        {
            DynamoDbOptions options = sp.GetRequiredService<IOptions<DynamoDbOptions>>().Value;
            var config = new AmazonDynamoDBConfig();

            if (!string.IsNullOrEmpty(options.ServiceUrl)) config.ServiceURL = options.ServiceUrl;

            if (!string.IsNullOrEmpty(options.Region))
                config.RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region);

            return new AmazonDynamoDBClient(config);
        });

        services.AddSingleton<IDynamoDbRepository, DynamoDbRepository>();

        return services;
    }
}

/// <summary>
///     Options for configuring DynamoDB
/// </summary>
public class DynamoDbOptions
{
    /// <summary>
    ///     DynamoDB service URL (for local development)
    /// </summary>
    public string? ServiceUrl { get; set; }

    /// <summary>
    ///     AWS region
    /// </summary>
    public string? Region { get; set; }
}