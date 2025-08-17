using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Amazon.CognitoIdentityProvider;
using Amazon.DynamoDBv2;
using Amazon.S3;
using Amazon.SQS;

namespace ThirdOpinion.Common.FunctionalTests.Utilities;

public static class TestCollectionSetupHelper
{

    public static IServiceCollection ConfigureAwsServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var region = configuration.GetValue<string>("AWS:Region") ?? "us-east-1";
        var regionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region);
        
        // Check for AWS Profile for local development
        var awsProfile = Environment.GetEnvironmentVariable("AWS_PROFILE");
        
        if (!string.IsNullOrEmpty(awsProfile))
        {
            // Use profile-based credentials for local development
            var sharedCredentialsFile = new Amazon.Runtime.CredentialManagement.SharedCredentialsFile();
            if (sharedCredentialsFile.TryGetProfile(awsProfile, out var profile))
            {
                var credentials = profile.GetAWSCredentials(sharedCredentialsFile);
                
                var cognitoClient = new AmazonCognitoIdentityProviderClient(credentials, regionEndpoint);
                services.AddSingleton(typeof(IAmazonCognitoIdentityProvider), cognitoClient);

                var dynamoClient = new AmazonDynamoDBClient(credentials, regionEndpoint);
                services.AddSingleton(typeof(IAmazonDynamoDB), dynamoClient);

                var s3Client = new AmazonS3Client(credentials, regionEndpoint);
                services.AddSingleton(typeof(IAmazonS3), s3Client);

                var sqsClient = new AmazonSQSClient(credentials, regionEndpoint);
                services.AddSingleton(typeof(IAmazonSQS), sqsClient);
            }
            else
            {
                throw new InvalidOperationException($"AWS Profile '{awsProfile}' not found in credentials file.");
            }
        }
        else
        {
            // Use default credential chain (environment variables, IAM roles, etc.)
            var cognitoClient = new AmazonCognitoIdentityProviderClient(regionEndpoint);
            services.AddSingleton(typeof(IAmazonCognitoIdentityProvider), cognitoClient);

            var dynamoClient = new AmazonDynamoDBClient(regionEndpoint);
            services.AddSingleton(typeof(IAmazonDynamoDB), dynamoClient);

            var s3Client = new AmazonS3Client(regionEndpoint);
            services.AddSingleton(typeof(IAmazonS3), s3Client);

            var sqsClient = new AmazonSQSClient(regionEndpoint);
            services.AddSingleton(typeof(IAmazonSQS), sqsClient);
        }

        return services;
    }

    public static IConfiguration BuildTestConfiguration(string? environment = null)
    {
        var configBuilder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile("appsettings.Test.json", optional: true, reloadOnChange: false);

        if (!string.IsNullOrEmpty(environment))
        {
            configBuilder.AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false);
        }

        configBuilder.AddEnvironmentVariables();

        return configBuilder.Build();
    }

    public static IServiceProvider BuildTestServiceProvider(IConfiguration configuration)
    {
        var services = new ServiceCollection();

        services.AddSingleton(configuration);
        services.AddLogging(builder =>
        {
            builder.AddConfiguration(configuration.GetSection("Logging"));
            builder.AddConsole();
            // Debug logging not essential for functional tests
        });

        services.ConfigureAwsServices(configuration);

        return services.BuildServiceProvider();
    }

    public static string GenerateTestResourceName(string prefix, string testName)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var randomSuffix = Guid.NewGuid().ToString("N")[..8];
        return $"{prefix}-{testName}-{timestamp}-{randomSuffix}";
    }

    public static TimeSpan GetTestTimeout(IConfiguration configuration)
    {
        var timeoutString = configuration.GetValue<string>("TestSettings:TestTimeout") ?? "00:05:00";
        return TimeSpan.Parse(timeoutString);
    }

    public static bool ShouldCleanupResources(IConfiguration configuration)
    {
        return configuration.GetValue<bool>("TestSettings:CleanupResources", true);
    }

    public static int GetMaxRetryAttempts(IConfiguration configuration)
    {
        return configuration.GetValue<int>("TestSettings:MaxRetryAttempts", 3);
    }

    public static TimeSpan GetRetryDelay(IConfiguration configuration)
    {
        var delayString = configuration.GetValue<string>("TestSettings:RetryDelay") ?? "00:00:02";
        return TimeSpan.Parse(delayString);
    }

    public static async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        int maxAttempts = 3,
        TimeSpan? delay = null,
        ILogger? logger = null)
    {
        var retryDelay = delay ?? TimeSpan.FromSeconds(2);
        Exception? lastException = null;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex)
            {
                lastException = ex;
                logger?.LogWarning(ex, "Attempt {Attempt} of {MaxAttempts} failed", attempt, maxAttempts);

                if (attempt < maxAttempts)
                {
                    await Task.Delay(retryDelay);
                }
            }
        }

        throw new InvalidOperationException(
            $"Operation failed after {maxAttempts} attempts. Last exception: {lastException?.Message}",
            lastException);
    }

    public static async Task ExecuteWithRetryAsync(
        Func<Task> operation,
        int maxAttempts = 3,
        TimeSpan? delay = null,
        ILogger? logger = null)
    {
        await ExecuteWithRetryAsync(async () =>
        {
            await operation();
            return true;
        }, maxAttempts, delay, logger);
    }

    public static bool IsRunningInCI()
    {
        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")) ||
               !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")) ||
               !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TF_BUILD"));
    }

    public static void ValidateTestEnvironment(IConfiguration configuration)
    {
        // Validate AWS credentials are available for real AWS testing
        var hasAwsCredentials = 
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID")) ||
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_PROFILE")) ||
            File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".aws", "credentials"));

        if (!hasAwsCredentials)
        {
            throw new InvalidOperationException(
                "AWS credentials not found. Configure AWS credentials using AWS CLI, environment variables, or IAM role.");
        }
        
        // Validate required configuration sections exist
        var requiredSections = new[] { "TestSettings", "AWS" };
        foreach (var section in requiredSections)
        {
            if (!configuration.GetSection(section).Exists())
            {
                throw new InvalidOperationException($"Required configuration section '{section}' is missing.");
            }
        }
    }
}