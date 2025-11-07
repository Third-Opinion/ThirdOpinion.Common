using Amazon.SQS;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ThirdOpinion.Common.Aws.SQS;

namespace ThirdOpinion.Common.Aws.Tests.SQS;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddSqsMessaging_WithConfiguration_RegistersServices()
    {
        // Arrange
        var services = new ServiceCollection();
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "SQS:ServiceUrl", "http://localhost:4566" },
                { "SQS:Region", "us-east-1" }
            })
            .Build();

        // Act
        services.AddLogging();
        services.AddSqsMessaging(configuration);
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        // Assert
        serviceProvider.GetService<IAmazonSQS>().ShouldNotBeNull();
        serviceProvider.GetService<ISqsMessageQueue>().ShouldNotBeNull();
        serviceProvider.GetService<ISqsMessageQueue>().ShouldBeOfType<SqsMessageQueue>();
    }

    [Fact]
    public void AddSqsMessaging_WithConfigurationAction_RegistersServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddLogging();
        services.AddSqsMessaging(options =>
        {
            options.ServiceUrl = "http://localhost:4566";
            options.Region = "us-west-2";
        });
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        // Assert
        serviceProvider.GetService<IAmazonSQS>().ShouldNotBeNull();
        serviceProvider.GetService<ISqsMessageQueue>().ShouldNotBeNull();

        var options = serviceProvider.GetService<IOptions<SqsOptions>>();
        options.ShouldNotBeNull();
        options.Value.ServiceUrl.ShouldBe("http://localhost:4566");
        options.Value.Region.ShouldBe("us-west-2");
    }

    [Fact]
    public void AddSqsMessaging_ConfiguresAmazonSQSClient_WithServiceUrl()
    {
        // Arrange
        var services = new ServiceCollection();
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "SQS:ServiceUrl", "http://localhost:4566" },
                { "SQS:Region", "us-east-1" }
            })
            .Build();

        // Act
        services.AddLogging();
        services.AddSqsMessaging(configuration);
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        // Assert
        var sqsClient = serviceProvider.GetService<IAmazonSQS>();
        sqsClient.ShouldNotBeNull();
        sqsClient.ShouldBeOfType<AmazonSQSClient>();
    }

    [Fact]
    public void AddSqsMessaging_ConfiguresAmazonSQSClient_WithRegionOnly()
    {
        // Arrange
        var services = new ServiceCollection();
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "SQS:Region", "eu-west-1" }
            })
            .Build();

        // Act
        services.AddLogging();
        services.AddSqsMessaging(configuration);
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        // Assert
        var sqsClient = serviceProvider.GetService<IAmazonSQS>();
        sqsClient.ShouldNotBeNull();
        sqsClient.ShouldBeOfType<AmazonSQSClient>();
    }

    [Fact]
    public void AddSqsMessaging_ConfiguresAmazonSQSClient_WithEmptyConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        // Act
        services.AddLogging();
        services.AddSqsMessaging(configuration);
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        // Assert
        var sqsClient = serviceProvider.GetService<IAmazonSQS>();
        sqsClient.ShouldNotBeNull();
        sqsClient.ShouldBeOfType<AmazonSQSClient>();
    }

    [Fact]
    public void AddSqsMessaging_RegistersServiceAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        // Act
        services.AddLogging();
        services.AddSqsMessaging(configuration);
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        // Assert
        var sqsClient1 = serviceProvider.GetService<IAmazonSQS>();
        var sqsClient2 = serviceProvider.GetService<IAmazonSQS>();
        var messageQueue1 = serviceProvider.GetService<ISqsMessageQueue>();
        var messageQueue2 = serviceProvider.GetService<ISqsMessageQueue>();

        sqsClient1.ShouldBeSameAs(sqsClient2);
        messageQueue1.ShouldBeSameAs(messageQueue2);
    }

    [Fact]
    public void AddSqsMessaging_ConfiguresOptionsCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "SQS:ServiceUrl", "https://sqs.custom-region.amazonaws.com" },
                { "SQS:Region", "custom-region" }
            })
            .Build();

        // Act
        services.AddLogging();
        services.AddSqsMessaging(configuration);
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        // Assert
        var options = serviceProvider.GetService<IOptions<SqsOptions>>();
        options.ShouldNotBeNull();
        options.Value.ServiceUrl.ShouldBe("https://sqs.custom-region.amazonaws.com");
        options.Value.Region.ShouldBe("custom-region");
    }

    [Fact]
    public void AddSqsMessaging_WithNullConfiguration_ThrowsException()
    {
        // Arrange
        var services = new ServiceCollection();
        IConfiguration nullConfiguration = null!;

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => services.AddSqsMessaging(nullConfiguration));
    }

    [Fact]
    public void AddSqsMessaging_WithNullConfigureAction_ThrowsException()
    {
        // Arrange
        var services = new ServiceCollection();
        Action<SqsOptions> nullAction = null!;

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => services.AddSqsMessaging(nullAction));
    }

    [Fact]
    public void AddSqsMessaging_WithValidRegion_ConfiguresRegionEndpoint()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddLogging();
        services.AddSqsMessaging(options => { options.Region = "ap-southeast-2"; });
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        // Assert
        var sqsClient = serviceProvider.GetService<IAmazonSQS>();
        sqsClient.ShouldNotBeNull();

        // Verify the client was created without throwing exceptions
        sqsClient.ShouldBeOfType<AmazonSQSClient>();
    }

    [Fact]
    public void AddSqsMessaging_WithInvalidRegion_StillCreatesClient()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        // This should not throw during registration
        services.AddLogging();
        services.AddSqsMessaging(options => { options.Region = "invalid-region"; });

        ServiceProvider serviceProvider = services.BuildServiceProvider();

        // The client creation might fail with invalid region, but registration should succeed
        // In practice, AWS SDK will handle invalid regions gracefully or throw at runtime
        serviceProvider.GetService<ISqsMessageQueue>().ShouldNotBeNull();
    }

    [Fact]
    public void SqsOptions_DefaultValues_AreCorrect()
    {
        // Act
        var options = new SqsOptions();

        // Assert
        options.ServiceUrl.ShouldBeNull();
        options.Region.ShouldBeNull();
    }

    [Fact]
    public void SqsOptions_CanSetProperties()
    {
        // Act
        var options = new SqsOptions
        {
            ServiceUrl = "http://test-url",
            Region = "test-region"
        };

        // Assert
        options.ServiceUrl.ShouldBe("http://test-url");
        options.Region.ShouldBe("test-region");
    }

    [Fact]
    public void AddSqsMessaging_ConfigurationBinding_WorksCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "SQS:ServiceUrl", "http://localhost:9324" },
                { "SQS:Region", "elasticmq" }
            })
            .Build();

        // Act
        services.AddLogging();
        services.AddSqsMessaging(configuration);
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        // Assert
        var options = serviceProvider.GetService<IOptions<SqsOptions>>();
        options.ShouldNotBeNull();
        options.Value.ServiceUrl.ShouldBe("http://localhost:9324");
        options.Value.Region.ShouldBe("elasticmq");
    }

    [Fact]
    public void AddSqsMessaging_MultipleCalls_DoesNotDuplicateRegistrations()
    {
        // Arrange
        var services = new ServiceCollection();
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        // Act
        services.AddLogging();
        services.AddSqsMessaging(configuration);
        services.AddSqsMessaging(configuration); // Second call

        ServiceProvider serviceProvider = services.BuildServiceProvider();

        // Assert
        int sqsServices = serviceProvider.GetServices<IAmazonSQS>().Count();
        int queueServices = serviceProvider.GetServices<ISqsMessageQueue>().Count();

        // The second registration will override the first, but both should resolve to the same instance due to Singleton lifetime
        sqsServices.ShouldBe(1);
        queueServices.ShouldBe(1);
    }
}