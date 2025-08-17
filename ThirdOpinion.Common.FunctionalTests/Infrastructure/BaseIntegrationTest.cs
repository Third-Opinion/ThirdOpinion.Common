using Amazon.CognitoIdentityProvider;
using Amazon.DynamoDBv2;
using Amazon.S3;
using Amazon.SQS;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ThirdOpinion.Common.FunctionalTests.Utilities;
using Xunit.Abstractions;

namespace ThirdOpinion.Common.FunctionalTests.Infrastructure;

/// <summary>
/// Base class for all integration tests providing common setup and teardown
/// </summary>
public abstract class BaseIntegrationTest : IAsyncLifetime
{
    protected readonly ITestOutputHelper Output;
    protected readonly IServiceProvider ServiceProvider;
    protected readonly IConfiguration Configuration;
    protected readonly ILogger Logger;
    
    // AWS Service Clients
    protected readonly IAmazonCognitoIdentityProvider CognitoClient;
    protected readonly IAmazonDynamoDB DynamoDbClient;
    protected readonly IAmazonS3 S3Client;
    protected readonly IAmazonSQS SqsClient;
    
    
    protected BaseIntegrationTest(ITestOutputHelper output)
    {
        Output = output;
        
        // Build configuration
        Configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.Test.json", optional: true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Test"}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
        
        // Setup dependency injection
        var services = new ServiceCollection();
        ConfigureServices(services);
        ServiceProvider = services.BuildServiceProvider();
        
        // Get logger
        var loggerFactory = ServiceProvider.GetRequiredService<ILoggerFactory>();
        Logger = loggerFactory.CreateLogger(GetType());
        
        // Get AWS clients
        CognitoClient = ServiceProvider.GetRequiredService(typeof(IAmazonCognitoIdentityProvider)) as IAmazonCognitoIdentityProvider;
        DynamoDbClient = ServiceProvider.GetRequiredService(typeof(IAmazonDynamoDB)) as IAmazonDynamoDB;
        S3Client = ServiceProvider.GetRequiredService(typeof(IAmazonS3)) as IAmazonS3;
        SqsClient = ServiceProvider.GetRequiredService(typeof(IAmazonSQS)) as IAmazonSQS;
    }

    /// <summary>
    /// Configure services for dependency injection
    /// </summary>
    protected virtual void ConfigureServices(IServiceCollection services)
    {
        // Add logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Debug);
        });

        // Add configuration
        services.AddSingleton(Configuration);

        // Configure real AWS services
        services.ConfigureAwsServices(Configuration);
    }



    /// <summary>
    /// Initialize test environment
    /// </summary>
    public virtual async Task InitializeAsync()
    {
        await SetupTestResourcesAsync();
        
        Logger.LogInformation("Test environment initialized for {TestClass}", GetType().Name);
    }

    /// <summary>
    /// Cleanup test environment
    /// </summary>
    public virtual async Task DisposeAsync()
    {
        try
        {
            await CleanupTestResourcesAsync();
            Logger.LogInformation("Test environment cleaned up for {TestClass}", GetType().Name);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error during test cleanup for {TestClass}", GetType().Name);
        }
        finally
        {
            if (ServiceProvider is IDisposable disposable)
                disposable.Dispose();
        }
    }


    /// <summary>
    /// Setup any test-specific resources
    /// </summary>
    protected virtual Task SetupTestResourcesAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Cleanup any test-specific resources
    /// </summary>
    protected virtual Task CleanupTestResourcesAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Generate a unique test resource name
    /// </summary>
    protected string GenerateTestResourceName(string prefix = "test")
    {
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
        var random = Guid.NewGuid().ToString("N")[..8];
        return $"{prefix}-{timestamp}-{random}".ToLowerInvariant();
    }

    /// <summary>
    /// Write output to test logs
    /// </summary>
    protected void WriteOutput(string message)
    {
        Output.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}");
        Logger.LogInformation(message);
    }
}