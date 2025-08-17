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
    public async Task ExecuteAsync_ReceivesAndProcessesMessages()
    {
        // Arrange
        var handler = new SqsMessageHandler(
            _sqsClientMock.Object,
            _loggerMock.Object,
            _configurationMock.Object,
            _serviceProviderMock.Object,
            _dynamoRepositoryMock.Object);

        var testMessages = new List<Message>
        {
            new() 
            { 
                MessageId = "msg-1", 
                Body = "test message body 1", 
                ReceiptHandle = "receipt-handle-1" 
            },
            new() 
            { 
                MessageId = "msg-2", 
                Body = "test message body 2", 
                ReceiptHandle = "receipt-handle-2" 
            }
        };

        var receiveResponse = new ReceiveMessageResponse
        {
            HttpStatusCode = HttpStatusCode.OK,
            Messages = testMessages
        };

        // Setup SQS client to return messages once, then empty response
        var callCount = 0;
        _sqsClientMock.Setup(x => x.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(() =>
                     {
                         callCount++;
                         if (callCount == 1)
                         {
                             return receiveResponse;
                         }
                         return new ReceiveMessageResponse 
                         { 
                             HttpStatusCode = HttpStatusCode.OK, 
                             Messages = new List<Message>() 
                         };
                     });

        _sqsClientMock.Setup(x => x.DeleteMessageAsync(It.IsAny<DeleteMessageRequest>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new DeleteMessageResponse { HttpStatusCode = HttpStatusCode.OK });

        var cancellationTokenSource = new CancellationTokenSource();
        
        // Act
        var executeTask = handler.StartAsync(cancellationTokenSource.Token);
        
        // Allow some processing time
        await Task.Delay(100);
        
        // Stop the service
        cancellationTokenSource.Cancel();
        
        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }

        // Assert
        _sqsClientMock.Verify(x => x.ReceiveMessageAsync(
            It.Is<ReceiveMessageRequest>(r => 
                r.QueueUrl == _testQueueUrl &&
                r.MaxNumberOfMessages == 10 &&
                r.WaitTimeSeconds == 20), 
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);

        _sqsClientMock.Verify(x => x.DeleteMessageAsync(
            It.Is<DeleteMessageRequest>(r => r.QueueUrl == _testQueueUrl), 
            It.IsAny<CancellationToken>()), Times.Exactly(2));

        VerifyLoggerInfoWasCalled("Processing message msg-1");
        VerifyLoggerInfoWasCalled("Processing message msg-2");
    }

    [Fact]
    public async Task ExecuteAsync_MessageProcessingThrowsException_LogsErrorButContinues()
    {
        // Arrange
        var handler = new TestSqsMessageHandler(
            _sqsClientMock.Object,
            _loggerMock.Object,
            _configurationMock.Object,
            _serviceProviderMock.Object,
            _dynamoRepositoryMock.Object,
            shouldThrowOnProcess: true);

        var testMessage = new Message 
        { 
            MessageId = "msg-error", 
            Body = "test message body", 
            ReceiptHandle = "receipt-handle-error" 
        };

        var receiveResponse = new ReceiveMessageResponse
        {
            HttpStatusCode = HttpStatusCode.OK,
            Messages = new List<Message> { testMessage }
        };

        var callCount = 0;
        _sqsClientMock.Setup(x => x.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(() =>
                     {
                         callCount++;
                         if (callCount == 1)
                         {
                             return receiveResponse;
                         }
                         return new ReceiveMessageResponse 
                         { 
                             HttpStatusCode = HttpStatusCode.OK, 
                             Messages = new List<Message>() 
                         };
                     });

        var cancellationTokenSource = new CancellationTokenSource();
        
        // Act
        var executeTask = handler.StartAsync(cancellationTokenSource.Token);
        
        await Task.Delay(100);
        cancellationTokenSource.Cancel();
        
        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert
        VerifyLoggerErrorWasCalled("Error processing message msg-error");
        
        // Verify delete was not called since processing failed
        _sqsClientMock.Verify(x => x.DeleteMessageAsync(
            It.IsAny<DeleteMessageRequest>(), 
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_SqsReceiveThrowsException_LogsErrorAndRetries()
    {
        // Arrange
        var handler = new SqsMessageHandler(
            _sqsClientMock.Object,
            _loggerMock.Object,
            _configurationMock.Object,
            _serviceProviderMock.Object,
            _dynamoRepositoryMock.Object);

        var callCount = 0;
        _sqsClientMock.Setup(x => x.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
                     .ThrowsAsync(new AmazonSQSException("SQS service error"));

        var cancellationTokenSource = new CancellationTokenSource();
        
        // Act
        var executeTask = handler.StartAsync(cancellationTokenSource.Token);
        
        await Task.Delay(200); // Allow time for error and retry
        cancellationTokenSource.Cancel();
        
        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert
        VerifyLoggerErrorWasCalled("Error receiving messages from SQS");
    }

    [Fact]
    public async Task ExecuteAsync_DeleteMessageThrowsException_LogsErrorButContinues()
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
            MessageId = "msg-delete-error", 
            Body = "test message body", 
            ReceiptHandle = "receipt-handle-delete-error" 
        };

        var receiveResponse = new ReceiveMessageResponse
        {
            HttpStatusCode = HttpStatusCode.OK,
            Messages = new List<Message> { testMessage }
        };

        var callCount = 0;
        _sqsClientMock.Setup(x => x.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(() =>
                     {
                         callCount++;
                         if (callCount == 1)
                         {
                             return receiveResponse;
                         }
                         return new ReceiveMessageResponse 
                         { 
                             HttpStatusCode = HttpStatusCode.OK, 
                             Messages = new List<Message>() 
                         };
                     });

        _sqsClientMock.Setup(x => x.DeleteMessageAsync(It.IsAny<DeleteMessageRequest>(), It.IsAny<CancellationToken>()))
                     .ThrowsAsync(new AmazonSQSException("Delete failed"));

        var cancellationTokenSource = new CancellationTokenSource();
        
        // Act
        var executeTask = handler.StartAsync(cancellationTokenSource.Token);
        
        await Task.Delay(100);
        cancellationTokenSource.Cancel();
        
        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert
        VerifyLoggerInfoWasCalled("Processing message msg-delete-error");
        VerifyLoggerErrorWasCalled("Error deleting message receipt-handle-delete-error");
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

// Test helper class to simulate processing failures
public class TestSqsMessageHandler : SqsMessageHandler
{
    private readonly bool _shouldThrowOnProcess;

    public TestSqsMessageHandler(
        IAmazonSQS sqsClient,
        ILogger<SqsMessageHandler> logger,
        IConfiguration configuration,
        IServiceProvider serviceProvider,
        IDynamoDbRepository dynamoRepository,
        bool shouldThrowOnProcess = false)
        : base(sqsClient, logger, configuration, serviceProvider, dynamoRepository)
    {
        _shouldThrowOnProcess = shouldThrowOnProcess;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Override to provide controlled behavior for testing
        if (_shouldThrowOnProcess)
        {
            // Simulate a receive that gets a message, processing fails
            var receiveResponse = new ReceiveMessageResponse
            {
                Messages = new List<Message>
                {
                    new() { MessageId = "msg-error", Body = "test", ReceiptHandle = "handle" }
                }
            };

            // Simulate processing the message and throwing an exception
            throw new Exception("Simulated processing error");
        }

        await base.ExecuteAsync(stoppingToken);
    }
}