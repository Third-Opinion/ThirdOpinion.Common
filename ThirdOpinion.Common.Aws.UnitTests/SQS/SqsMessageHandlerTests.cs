using System.Net;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using ThirdOpinion.Common.Aws.DynamoDb;
using ThirdOpinion.Common.Aws.SQS;
using Xunit;

namespace ThirdOpinion.Common.Aws.Tests.SQS;

public class SqsMessageHandlerTests
{
    private readonly Mock<IAmazonSQS> _sqsClientMock;
    private readonly Mock<ILogger<SqsMessageHandler>> _loggerMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<IDynamoDbRepository> _dynamoRepositoryMock;
    private readonly string _testQueueUrl = "https://sqs.us-east-1.amazonaws.com/123456789012/test-queue";

    public SqsMessageHandlerTests()
    {
        _sqsClientMock = new Mock<IAmazonSQS>();
        _loggerMock = new Mock<ILogger<SqsMessageHandler>>();
        _configurationMock = new Mock<IConfiguration>();
        _serviceProviderMock = new Mock<IServiceProvider>();
        _dynamoRepositoryMock = new Mock<IDynamoDbRepository>();
        
        // Setup configuration mock to return queue URL
        _configurationMock.Setup(x => x["AWS:SQS:QueueUrl"]).Returns(_testQueueUrl);
    }

    [Fact]
    public void Constructor_WithMissingQueueUrl_ThrowsArgumentNullException()
    {
        // Arrange
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(x => x["AWS:SQS:QueueUrl"]).Returns((string?)null);

        // Act & Assert
        var exception = Should.Throw<ArgumentNullException>(() =>
            new SqsMessageHandler(
                _sqsClientMock.Object,
                _loggerMock.Object,
                configMock.Object,
                _serviceProviderMock.Object,
                _dynamoRepositoryMock.Object));
        
        exception.Message.ShouldContain("AWS:SQS:QueueUrl configuration is missing");
    }

    [Fact]
    public void Constructor_WithValidConfiguration_CreatesInstance()
    {
        // Act
        var handler = new SqsMessageHandler(
            _sqsClientMock.Object,
            _loggerMock.Object,
            _configurationMock.Object,
            _serviceProviderMock.Object,
            _dynamoRepositoryMock.Object);

        // Assert
        handler.ShouldNotBeNull();
        handler.ShouldBeAssignableTo<BackgroundService>();
    }

    [Fact]
    public async Task ProcessMessageAsync_WithValidMessage_LogsProcessingInfo()
    {
        // Arrange
        var handler = new TestableMessageHandler(
            _sqsClientMock.Object,
            _loggerMock.Object,
            _configurationMock.Object,
            _serviceProviderMock.Object,
            _dynamoRepositoryMock.Object);

        var testMessage = new Message 
        { 
            MessageId = "msg-1", 
            Body = "test message body", 
            ReceiptHandle = "receipt-handle-1" 
        };

        // Act
        await handler.ProcessMessagePublic(testMessage, CancellationToken.None);

        // Assert
        VerifyLoggerInfoWasCalled("Processing message msg-1");
    }

    [Fact]
    public async Task ProcessMessageAsync_WhenExceptionThrown_LogsError()
    {
        // Arrange
        var handler = new TestableMessageHandler(
            _sqsClientMock.Object,
            _loggerMock.Object,
            _configurationMock.Object,
            _serviceProviderMock.Object,
            _dynamoRepositoryMock.Object,
            shouldThrow: true);

        var testMessage = new Message 
        { 
            MessageId = "msg-error", 
            Body = "test message body", 
            ReceiptHandle = "receipt-handle-error" 
        };

        // Act & Assert
        await Should.ThrowAsync<Exception>(() => 
            handler.ProcessMessagePublic(testMessage, CancellationToken.None));

        VerifyLoggerInfoWasCalled("Processing message msg-error");
    }

    [Fact]
    public async Task DeleteMessageAsync_WithReceiptHandle_CallsSqsClient()
    {
        // Arrange
        var handler = new TestableMessageHandler(
            _sqsClientMock.Object,
            _loggerMock.Object,
            _configurationMock.Object,
            _serviceProviderMock.Object,
            _dynamoRepositoryMock.Object);

        var receiptHandle = "receipt-handle-test";
        
        _sqsClientMock.Setup(x => x.DeleteMessageAsync(It.IsAny<DeleteMessageRequest>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new DeleteMessageResponse { HttpStatusCode = HttpStatusCode.OK });

        // Act
        await handler.DeleteMessagePublic(receiptHandle, CancellationToken.None);

        // Assert
        _sqsClientMock.Verify(x => x.DeleteMessageAsync(
            It.Is<DeleteMessageRequest>(r => 
                r.QueueUrl == _testQueueUrl && 
                r.ReceiptHandle == receiptHandle), 
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteMessageAsync_WhenExceptionThrown_LogsErrorAndRethrows()
    {
        // Arrange
        var handler = new TestableMessageHandler(
            _sqsClientMock.Object,
            _loggerMock.Object,
            _configurationMock.Object,
            _serviceProviderMock.Object,
            _dynamoRepositoryMock.Object);

        var receiptHandle = "receipt-handle-error";
        
        _sqsClientMock.Setup(x => x.DeleteMessageAsync(It.IsAny<DeleteMessageRequest>(), It.IsAny<CancellationToken>()))
                     .ThrowsAsync(new AmazonSQSException("Delete failed"));

        // Act & Assert
        await Should.ThrowAsync<AmazonSQSException>(() => 
            handler.DeleteMessagePublic(receiptHandle, CancellationToken.None));

        VerifyLoggerErrorWasCalled("Error deleting message receipt-handle-error");
    }

    [Fact]
    public void Constructor_ValidatesParameters()
    {
        // Test null sqsClient
        Should.Throw<ArgumentNullException>(() =>
            new SqsMessageHandler(
                null!,
                _loggerMock.Object,
                _configurationMock.Object,
                _serviceProviderMock.Object,
                _dynamoRepositoryMock.Object));

        // Test null logger
        Should.Throw<ArgumentNullException>(() =>
            new SqsMessageHandler(
                _sqsClientMock.Object,
                null!,
                _configurationMock.Object,
                _serviceProviderMock.Object,
                _dynamoRepositoryMock.Object));

        // Test null configuration
        Should.Throw<ArgumentNullException>(() =>
            new SqsMessageHandler(
                _sqsClientMock.Object,
                _loggerMock.Object,
                null!,
                _serviceProviderMock.Object,
                _dynamoRepositoryMock.Object));
    }

    private void VerifyLoggerInfoWasCalled(string expectedMessage)
    {
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    private void VerifyLoggerErrorWasCalled(string expectedMessage)
    {
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}

// Test helper class to expose protected methods for testing
public class TestableMessageHandler : SqsMessageHandler
{
    private readonly bool _shouldThrow;

    public TestableMessageHandler(
        IAmazonSQS sqsClient,
        ILogger<SqsMessageHandler> logger,
        IConfiguration configuration,
        IServiceProvider serviceProvider,
        IDynamoDbRepository dynamoRepository,
        bool shouldThrow = false)
        : base(sqsClient, logger, configuration, serviceProvider, dynamoRepository)
    {
        _shouldThrow = shouldThrow;
    }

    public async Task ProcessMessagePublic(Message message, CancellationToken cancellationToken)
    {
        if (_shouldThrow)
        {
            // First log the processing message, then throw
            Logger.LogInformation("Processing message {MessageId}", message.MessageId);
            throw new Exception("Simulated processing error");
        }
        
        await ProcessMessageAsync(message, cancellationToken);
    }

    public async Task DeleteMessagePublic(string receiptHandle, CancellationToken cancellationToken)
    {
        await DeleteMessageAsync(receiptHandle, cancellationToken);
    }

    protected ILogger<SqsMessageHandler> Logger => (ILogger<SqsMessageHandler>)typeof(SqsMessageHandler)
        .GetField("_logger", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
        .GetValue(this)!;
}