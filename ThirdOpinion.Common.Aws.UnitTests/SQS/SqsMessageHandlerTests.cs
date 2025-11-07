using System.Net;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using ThirdOpinion.Common.Aws.DynamoDb;
using ThirdOpinion.Common.Aws.SQS;

namespace ThirdOpinion.Common.Aws.Tests.SQS;

public class SqsMessageHandlerTests
{
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<IDynamoDbRepository> _dynamoRepositoryMock;
    private readonly Mock<ILogger<SqsMessageHandler>> _loggerMock;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<IAmazonSQS> _sqsClientMock;

    private readonly string _testQueueUrl
        = "https://sqs.us-east-1.amazonaws.com/123456789012/test-queue";

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
    public async Task ProcessMessageAsync_ValidMessage_LogsProcessingInfo()
    {
        // Arrange
        var handler = new SqsMessageHandler(
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

        CancellationToken cancellationToken = CancellationToken.None;

        // Act
        await handler.ProcessMessageAsync(testMessage, cancellationToken);

        // Assert
        VerifyLoggerInfoWasCalled("Processing message msg-1");
    }

    [Fact]
    public async Task DeleteMessageAsync_ValidReceiptHandle_CallsSqsDelete()
    {
        // Arrange
        var handler = new SqsMessageHandler(
            _sqsClientMock.Object,
            _loggerMock.Object,
            _configurationMock.Object,
            _serviceProviderMock.Object,
            _dynamoRepositoryMock.Object);

        var receiptHandle = "receipt-handle-1";
        CancellationToken cancellationToken = CancellationToken.None;

        _sqsClientMock.Setup(x =>
                x.DeleteMessageAsync(It.IsAny<DeleteMessageRequest>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteMessageResponse { HttpStatusCode = HttpStatusCode.OK });

        // Act
        await handler.DeleteMessageAsync(receiptHandle, cancellationToken);

        // Assert
        _sqsClientMock.Verify(x => x.DeleteMessageAsync(
            It.Is<DeleteMessageRequest>(r =>
                r.QueueUrl == _testQueueUrl &&
                r.ReceiptHandle == receiptHandle),
            cancellationToken), Times.Once);
    }

    [Fact]
    public async Task ProcessMessageAsync_WithNullMessage_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new SqsMessageHandler(
            _sqsClientMock.Object,
            _loggerMock.Object,
            _configurationMock.Object,
            _serviceProviderMock.Object,
            _dynamoRepositoryMock.Object);

        CancellationToken cancellationToken = CancellationToken.None;

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(() =>
            handler.ProcessMessageAsync(null!, cancellationToken));
    }

    [Fact]
    public async Task DeleteMessageAsync_SqsThrowsException_LogsErrorAndRethrows()
    {
        // Arrange
        var handler = new SqsMessageHandler(
            _sqsClientMock.Object,
            _loggerMock.Object,
            _configurationMock.Object,
            _serviceProviderMock.Object,
            _dynamoRepositoryMock.Object);

        var receiptHandle = "receipt-handle-error";
        CancellationToken cancellationToken = CancellationToken.None;
        var sqsException = new AmazonSQSException("Delete failed");

        _sqsClientMock.Setup(x =>
                x.DeleteMessageAsync(It.IsAny<DeleteMessageRequest>(),
                    It.IsAny<CancellationToken>()))
            .ThrowsAsync(sqsException);

        // Act & Assert
        var exception = await Should.ThrowAsync<AmazonSQSException>(() =>
            handler.DeleteMessageAsync(receiptHandle, cancellationToken));

        exception.ShouldBe(sqsException);
        VerifyLoggerErrorWasCalled($"Error deleting message {receiptHandle}");
    }

    [Fact]
    public async Task DeleteMessageAsync_WithNullOrEmptyReceiptHandle_ThrowsArgumentException()
    {
        // Arrange
        var handler = new SqsMessageHandler(
            _sqsClientMock.Object,
            _loggerMock.Object,
            _configurationMock.Object,
            _serviceProviderMock.Object,
            _dynamoRepositoryMock.Object);

        CancellationToken cancellationToken = CancellationToken.None;

        // Act & Assert - null receipt handle
        await Should.ThrowAsync<ArgumentException>(() =>
            handler.DeleteMessageAsync(null!, cancellationToken));

        // Act & Assert - empty receipt handle
        await Should.ThrowAsync<ArgumentException>(() =>
            handler.DeleteMessageAsync(string.Empty, cancellationToken));
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