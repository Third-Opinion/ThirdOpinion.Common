using Amazon;
using Amazon.Bedrock;
using Amazon.BedrockRuntime;
using Amazon.CognitoIdentityProvider;
using Amazon.DynamoDBv2;
using Amazon.HealthLake;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using Amazon.S3;
using Amazon.SecretsManager;
using Amazon.SQS;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ThirdOpinion.Common.Aws.Bedrock;
using ThirdOpinion.Common.Aws.Misc.SecretsManager;
using ThirdOpinion.Common.Langfuse;
using ThirdOpinion.Common.Langfuse.Configuration;

namespace ThirdOpinion.Common.FunctionalTests.Utilities;

public static class TestCollectionSetupHelper
{
    public static IServiceCollection ConfigureAwsServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string region = configuration.GetValue<string>("AWS:Region") ?? "us-east-1";
        RegionEndpoint? regionEndpoint = RegionEndpoint.GetBySystemName(region);

        // Require AWS Profile - no fallback to access keys
        string awsProfile = Environment.GetEnvironmentVariable("AWS_PROFILE") ?? "to-dev-admin";

        // Validate SSO profile is configured
        ValidateSSOProfile(awsProfile);

        // Use profile-based credentials only - SSO required
        var sharedCredentialsFile = new SharedCredentialsFile();
        if (!sharedCredentialsFile.TryGetProfile(awsProfile, out CredentialProfile? profile))
            throw new InvalidOperationException(
                $"AWS SSO Profile '{awsProfile}' not found in credentials file. " +
                $"Please configure SSO by running: aws configure sso --profile {awsProfile}");

        AWSCredentials? credentials = profile.GetAWSCredentials(sharedCredentialsFile);

        // Configure all AWS service clients with SSO profile credentials
        var cognitoClient = new AmazonCognitoIdentityProviderClient(credentials, regionEndpoint);
        services.AddSingleton(typeof(IAmazonCognitoIdentityProvider), cognitoClient);

        var dynamoClient = new AmazonDynamoDBClient(credentials, regionEndpoint);
        services.AddSingleton(typeof(IAmazonDynamoDB), dynamoClient);

        var s3Client = new AmazonS3Client(credentials, regionEndpoint);
        services.AddSingleton(typeof(IAmazonS3), s3Client);

        var sqsClient = new AmazonSQSClient(credentials, regionEndpoint);
        services.AddSingleton(typeof(IAmazonSQS), sqsClient);

        var bedrockClient = new AmazonBedrockClient(credentials, regionEndpoint);
        services.AddSingleton(typeof(IAmazonBedrock), bedrockClient);

        var bedrockRuntimeClient = new AmazonBedrockRuntimeClient(credentials, regionEndpoint);
        services.AddSingleton(typeof(IAmazonBedrockRuntime), bedrockRuntimeClient);

        var healthLakeClient = new AmazonHealthLakeClient(credentials, regionEndpoint);
        services.AddSingleton(typeof(IAmazonHealthLake), healthLakeClient);

        var secretsManagerClient = new AmazonSecretsManagerClient(credentials, regionEndpoint);
        services.AddSingleton(typeof(IAmazonSecretsManager), secretsManagerClient);

        // Configure ThirdOpinion services with options patterns
        ConfigureThirdOpinionServices(services, configuration);

        return services;
    }

    private static void ConfigureThirdOpinionServices(IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure Langfuse if keys are provided
        var langfusePublicKey = configuration.GetValue<string>("Langfuse:PublicKey");
        var langfuseSecretKey = configuration.GetValue<string>("Langfuse:SecretKey");

        if (!string.IsNullOrEmpty(langfusePublicKey) && !string.IsNullOrEmpty(langfuseSecretKey))
        {
            services.Configure<LangfuseConfiguration>(configuration.GetSection("Langfuse"));
            services.AddSingleton<ILangfuseService, LangfuseService>();
        }

        // Configure Bedrock service
        services.AddSingleton<IBedrockService, BedrockService>();

        // Configure Secrets Manager service
        services.AddSingleton<ISecretsManagerService, SecretsManagerService>();
    }

    private static void ValidateSSOProfile(string profileName)
    {
        // Explicitly reject AWS access key environment variables
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID")) ||
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY")))
            throw new InvalidOperationException(
                "AWS access key environment variables detected but are not supported. " +
                "This application requires SSO authentication only. " +
                "Please unset AWS_ACCESS_KEY_ID and AWS_SECRET_ACCESS_KEY environment variables " +
                $"and use SSO profile '{profileName}' instead.");

        // Check if profile exists
        var chain = new CredentialProfileStoreChain();
        if (!chain.TryGetProfile(profileName, out CredentialProfile? profile))
            throw new InvalidOperationException(
                $"AWS SSO profile '{profileName}' not configured. " +
                $"Please run: aws configure sso --profile {profileName}");

        // Verify it's an SSO profile
        if (profile.Options.SsoAccountId == null || profile.Options.SsoRoleName == null)
            throw new InvalidOperationException(
                $"Profile '{profileName}' exists but is not an SSO profile. " +
                $"Please configure SSO by running: aws configure sso --profile {profileName}");
    }

    public static IConfiguration BuildTestConfiguration(string? environment = null)
    {
        IConfigurationBuilder configBuilder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", true, false)
            .AddJsonFile("appsettings.Test.json", true, false);

        if (!string.IsNullOrEmpty(environment))
            configBuilder.AddJsonFile($"appsettings.{environment}.json", true, false);

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
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string randomSuffix = Guid.NewGuid().ToString("N")[..8];
        return $"{prefix}-{testName}-{timestamp}-{randomSuffix}";
    }

    public static TimeSpan GetTestTimeout(IConfiguration configuration)
    {
        string timeoutString
            = configuration.GetValue<string>("TestSettings:TestTimeout") ?? "00:05:00";
        return TimeSpan.Parse(timeoutString);
    }

    public static bool ShouldCleanupResources(IConfiguration configuration)
    {
        return configuration.GetValue("TestSettings:CleanupResources", true);
    }

    public static int GetMaxRetryAttempts(IConfiguration configuration)
    {
        return configuration.GetValue("TestSettings:MaxRetryAttempts", 3);
    }

    public static TimeSpan GetRetryDelay(IConfiguration configuration)
    {
        string delayString
            = configuration.GetValue<string>("TestSettings:RetryDelay") ?? "00:00:02";
        return TimeSpan.Parse(delayString);
    }

    public static async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        int maxAttempts = 3,
        TimeSpan? delay = null,
        ILogger? logger = null)
    {
        TimeSpan retryDelay = delay ?? TimeSpan.FromSeconds(2);
        Exception? lastException = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
            try
            {
                return await operation();
            }
            catch (Exception ex)
            {
                lastException = ex;
                logger?.LogWarning(ex, "Attempt {Attempt} of {MaxAttempts} failed", attempt,
                    maxAttempts);

                if (attempt < maxAttempts) await Task.Delay(retryDelay);
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
        // Validate SSO profile is configured (no access keys allowed)
        string awsProfile = Environment.GetEnvironmentVariable("AWS_PROFILE") ?? "to-dev-admin";

        // Explicitly reject AWS access key environment variables
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID")) ||
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY")))
            throw new InvalidOperationException(
                "AWS access key environment variables detected but are not supported. " +
                "This application requires SSO authentication only. " +
                "Please unset AWS_ACCESS_KEY_ID and AWS_SECRET_ACCESS_KEY environment variables " +
                $"and use SSO profile '{awsProfile}' instead.");

        // Check if credentials file exists
        string credentialsFile
            = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".aws",
                "credentials");
        string configFile
            = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".aws",
                "config");

        if (!File.Exists(credentialsFile) && !File.Exists(configFile))
            throw new InvalidOperationException(
                $"AWS configuration files not found. Please configure SSO by running: aws configure sso --profile {awsProfile}");

        // Validate required configuration sections exist
        var requiredSections = new[] { "TestSettings", "AWS" };
        foreach (string section in requiredSections)
            if (!configuration.GetSection(section).Exists())
                throw new InvalidOperationException(
                    $"Required configuration section '{section}' is missing.");
    }
}