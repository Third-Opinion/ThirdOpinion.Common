using System.Net;
using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Logging;
using Moq;
using ThirdOpinion.Common.Aws.SQS;

namespace ThirdOpinion.Common.Aws.Tests.SQS;

public class SqsMessageQueueTests
{
    private readonly Mock<ILogger<SqsMessageQueue>> _loggerMock;
    private readonly SqsMessageQueue _messageQueue;
    private readonly Mock<IAmazonSQS> _sqsClientMock;

    public SqsMessageQueueTests()
    {
        _sqsClientMock = new Mock<IAmazonSQS>();
        _loggerMock = new Mock<ILogger<SqsMessageQueue>>();
        _messageQueue = new SqsMessageQueue(_sqsClientMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task SendMessageAsync_WithGenericType_SerializesAndSendsMessage()
    {
        // Arrange
        var queueUrl = "https://sqs.us-east-1.amazonaws.com/123456789012/test-queue";
        var message = new TestMessage { Id = "test-id", Content = "test content" };
        var expectedResponse = new SendMessageResponse
        {
            HttpStatusCode = HttpStatusCode.OK,
            MessageId = "msg-123"
        };

        _sqsClientMock.Setup(x =>
                x.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _messageQueue.SendMessageAsync(queueUrl, message);

        // Assert
        result.ShouldBe(expectedResponse);
        _sqsClientMock.Verify(x => x.SendMessageAsync(
            It.Is<SendMessageRequest>(r =>
                r.QueueUrl == queueUrl &&
                r.MessageBody.Contains("test-id") &&
                r.MessageBody.Contains("test content")),
            It.IsAny<CancellationToken>()), Times.Once);

        VerifyLoggerDebugWasCalled($"Sent message to queue {queueUrl}, MessageId: msg-123");
    }

    [Fact]
    public async Task SendMessageAsync_WithStringMessage_SendsDirectly()
    {
        // Arrange
        var queueUrl = "https://sqs.us-east-1.amazonaws.com/123456789012/test-queue";
        var messageBody = "plain text message";
        var messageAttributes = new Dictionary<string, MessageAttributeValue>
        {
            {
                "ContentType",
                new MessageAttributeValue { StringValue = "text/plain", DataType = "String" }
            }
        };
        var delaySeconds = 30;

        var expectedResponse = new SendMessageResponse
        {
            HttpStatusCode = HttpStatusCode.OK,
            MessageId = "msg-456"
        };

        _sqsClientMock.Setup(x =>
                x.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result
            = await _messageQueue.SendMessageAsync(queueUrl, messageBody, messageAttributes,
                delaySeconds);

        // Assert
        result.ShouldBe(expectedResponse);
        _sqsClientMock.Verify(x => x.SendMessageAsync(
            It.Is<SendMessageRequest>(r =>
                r.QueueUrl == queueUrl &&
                r.MessageBody == messageBody &&
                r.DelaySeconds == delaySeconds &&
                r.MessageAttributes.ContainsKey("ContentType")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendMessageAsync_Exception_LogsErrorAndRethrows()
    {
        // Arrange
        var queueUrl = "https://sqs.us-east-1.amazonaws.com/123456789012/test-queue";
        var messageBody = "test message";
        var expectedException = new AmazonSQSException("SQS error");

        _sqsClientMock.Setup(x =>
                x.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var exception = await Should.ThrowAsync<AmazonSQSException>(() =>
            _messageQueue.SendMessageAsync(queueUrl, messageBody));

        exception.ShouldBe(expectedException);
        VerifyLoggerErrorWasCalled($"Error sending message to queue {queueUrl}");
    }

    [Fact]
    public async Task SendMessageBatchAsync_WithGenericMessages_SerializesAndSendsBatch()
    {
        // Arrange
        var queueUrl = "https://sqs.us-east-1.amazonaws.com/123456789012/test-queue";
        var messages = new List<TestMessage>
        {
            new() { Id = "msg-1", Content = "content 1" },
            new() { Id = "msg-2", Content = "content 2" },
            new() { Id = "msg-3", Content = "content 3" }
        };

        var expectedResponse = new SendMessageBatchResponse
        {
            HttpStatusCode = HttpStatusCode.OK,
            Successful = new List<SendMessageBatchResultEntry>
            {
                new() { Id = "0", MessageId = "msg-batch-1" },
                new() { Id = "1", MessageId = "msg-batch-2" },
                new() { Id = "2", MessageId = "msg-batch-3" }
            },
            Failed = new List<BatchResultErrorEntry>()
        };

        _sqsClientMock.Setup(x =>
                x.SendMessageBatchAsync(It.IsAny<SendMessageBatchRequest>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _messageQueue.SendMessageBatchAsync(queueUrl, messages);

        // Assert
        result.ShouldBe(expectedResponse);
        _sqsClientMock.Verify(x => x.SendMessageBatchAsync(
            It.Is<SendMessageBatchRequest>(r =>
                r.QueueUrl == queueUrl &&
                r.Entries.Count == 3),
            It.IsAny<CancellationToken>()), Times.Once);

        VerifyLoggerDebugWasCalled($"Sent batch of 3 messages to queue {queueUrl}");
    }

    [Fact]
    public async Task SendMessageBatchAsync_WithFailedMessages_LogsWarning()
    {
        // Arrange
        var queueUrl = "https://sqs.us-east-1.amazonaws.com/123456789012/test-queue";
        var entries = new List<SendMessageBatchRequestEntry>
        {
            new() { Id = "1", MessageBody = "message 1" },
            new() { Id = "2", MessageBody = "message 2" }
        };

        var expectedResponse = new SendMessageBatchResponse
        {
            HttpStatusCode = HttpStatusCode.OK,
            Successful = new List<SendMessageBatchResultEntry>
            {
                new() { Id = "1", MessageId = "msg-success" }
            },
            Failed = new List<BatchResultErrorEntry>
            {
                new() { Id = "2", Code = "Error", Message = "Failed to send" }
            }
        };

        _sqsClientMock.Setup(x =>
                x.SendMessageBatchAsync(It.IsAny<SendMessageBatchRequest>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _messageQueue.SendMessageBatchAsync(queueUrl, entries);

        // Assert
        result.ShouldBe(expectedResponse);
        VerifyLoggerWarningWasCalled($"Failed to send 1 messages to queue {queueUrl}");
    }

    [Fact]
    public async Task ReceiveMessagesAsync_SuccessfulReceive_ReturnsMessages()
    {
        // Arrange
        var queueUrl = "https://sqs.us-east-1.amazonaws.com/123456789012/test-queue";
        var maxMessages = 5;
        var waitTimeSeconds = 20;
        var visibilityTimeout = 30;

        var expectedResponse = new ReceiveMessageResponse
        {
            HttpStatusCode = HttpStatusCode.OK,
            Messages = new List<Message>
            {
                new() { MessageId = "msg-1", Body = "message body 1", ReceiptHandle = "handle-1" },
                new() { MessageId = "msg-2", Body = "message body 2", ReceiptHandle = "handle-2" }
            }
        };

        _sqsClientMock.Setup(x =>
                x.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _messageQueue.ReceiveMessagesAsync(queueUrl, maxMessages,
            waitTimeSeconds, visibilityTimeout);

        // Assert
        result.ShouldBe(expectedResponse);
        _sqsClientMock.Verify(x => x.ReceiveMessageAsync(
            It.Is<ReceiveMessageRequest>(r =>
                r.QueueUrl == queueUrl &&
                r.MaxNumberOfMessages == maxMessages &&
                r.WaitTimeSeconds == waitTimeSeconds &&
                r.VisibilityTimeout == visibilityTimeout &&
                r.MessageSystemAttributeNames.Contains("All") &&
                r.MessageAttributeNames.Contains("All")),
            It.IsAny<CancellationToken>()), Times.Once);

        VerifyLoggerDebugWasCalled($"Received 2 messages from queue {queueUrl}");
    }

    [Fact]
    public async Task ReceiveMessagesAsync_WithMessageHandler_ProcessesMessages()
    {
        // Arrange
        var queueUrl = "https://sqs.us-east-1.amazonaws.com/123456789012/test-queue";
        var processedMessages = new List<TestMessage>();

        Func<TestMessage, Task<bool>> messageHandler = async msg =>
        {
            processedMessages.Add(msg);
            await Task.Delay(1);
            return true; // Delete the message
        };

        var testMessage1 = new TestMessage { Id = "test-1", Content = "content 1" };
        var testMessage2 = new TestMessage { Id = "test-2", Content = "content 2" };

        var receiveResponse = new ReceiveMessageResponse
        {
            HttpStatusCode = HttpStatusCode.OK,
            Messages = new List<Message>
            {
                new()
                {
                    MessageId = "msg-1",
                    Body = JsonSerializer.Serialize(testMessage1,
                        new JsonSerializerOptions
                            { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                    ReceiptHandle = "handle-1"
                },
                new()
                {
                    MessageId = "msg-2",
                    Body = JsonSerializer.Serialize(testMessage2,
                        new JsonSerializerOptions
                            { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                    ReceiptHandle = "handle-2"
                }
            }
        };

        _sqsClientMock.Setup(x =>
                x.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(receiveResponse);

        _sqsClientMock.Setup(x => x.DeleteMessageBatchAsync(It.IsAny<DeleteMessageBatchRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteMessageBatchResponse { HttpStatusCode = HttpStatusCode.OK });

        // Act
        await _messageQueue.ReceiveMessagesAsync(queueUrl, messageHandler);

        // Assert
        processedMessages.Count.ShouldBe(2);
        processedMessages.ShouldContain(m => m.Id == "test-1" && m.Content == "content 1");
        processedMessages.ShouldContain(m => m.Id == "test-2" && m.Content == "content 2");

        _sqsClientMock.Verify(x => x.DeleteMessageBatchAsync(
            It.Is<DeleteMessageBatchRequest>(r => r.Entries.Count == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteMessageAsync_SuccessfulDelete_ReturnsResponse()
    {
        // Arrange
        var queueUrl = "https://sqs.us-east-1.amazonaws.com/123456789012/test-queue";
        var receiptHandle = "receipt-handle-123";

        var expectedResponse = new DeleteMessageResponse { HttpStatusCode = HttpStatusCode.OK };
        _sqsClientMock.Setup(x =>
                x.DeleteMessageAsync(It.IsAny<DeleteMessageRequest>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _messageQueue.DeleteMessageAsync(queueUrl, receiptHandle);

        // Assert
        result.ShouldBe(expectedResponse);
        _sqsClientMock.Verify(x => x.DeleteMessageAsync(
            It.Is<DeleteMessageRequest>(r =>
                r.QueueUrl == queueUrl &&
                r.ReceiptHandle == receiptHandle),
            It.IsAny<CancellationToken>()), Times.Once);

        VerifyLoggerDebugWasCalled($"Deleted message from queue {queueUrl}");
    }

    [Fact]
    public async Task DeleteMessageBatchAsync_SuccessfulDelete_ReturnsResponse()
    {
        // Arrange
        var queueUrl = "https://sqs.us-east-1.amazonaws.com/123456789012/test-queue";
        var entries = new List<DeleteMessageBatchRequestEntry>
        {
            new() { Id = "1", ReceiptHandle = "handle-1" },
            new() { Id = "2", ReceiptHandle = "handle-2" }
        };

        var expectedResponse = new DeleteMessageBatchResponse
        {
            HttpStatusCode = HttpStatusCode.OK,
            Successful = new List<DeleteMessageBatchResultEntry>
            {
                new() { Id = "1" },
                new() { Id = "2" }
            },
            Failed = new List<BatchResultErrorEntry>()
        };

        _sqsClientMock.Setup(x => x.DeleteMessageBatchAsync(It.IsAny<DeleteMessageBatchRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _messageQueue.DeleteMessageBatchAsync(queueUrl, entries);

        // Assert
        result.ShouldBe(expectedResponse);
        VerifyLoggerDebugWasCalled($"Deleted batch of 2 messages from queue {queueUrl}");
    }

    [Fact]
    public async Task ChangeMessageVisibilityAsync_ValidRequest_ReturnsResponse()
    {
        // Arrange
        var queueUrl = "https://sqs.us-east-1.amazonaws.com/123456789012/test-queue";
        var receiptHandle = "receipt-handle-123";
        var visibilityTimeout = 60;

        var expectedResponse = new ChangeMessageVisibilityResponse
            { HttpStatusCode = HttpStatusCode.OK };
        _sqsClientMock.Setup(x =>
                x.ChangeMessageVisibilityAsync(It.IsAny<ChangeMessageVisibilityRequest>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result
            = await _messageQueue.ChangeMessageVisibilityAsync(queueUrl, receiptHandle,
                visibilityTimeout);

        // Assert
        result.ShouldBe(expectedResponse);
        _sqsClientMock.Verify(x => x.ChangeMessageVisibilityAsync(
            It.Is<ChangeMessageVisibilityRequest>(r =>
                r.QueueUrl == queueUrl &&
                r.ReceiptHandle == receiptHandle &&
                r.VisibilityTimeout == visibilityTimeout),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetQueueAttributesAsync_ValidRequest_ReturnsAttributes()
    {
        // Arrange
        var queueUrl = "https://sqs.us-east-1.amazonaws.com/123456789012/test-queue";
        var attributeNames = new List<string> { "VisibilityTimeout", "MessageRetentionPeriod" };

        var expectedResponse = new GetQueueAttributesResponse
        {
            HttpStatusCode = HttpStatusCode.OK,
            Attributes = new Dictionary<string, string>
            {
                { "VisibilityTimeout", "30" },
                { "MessageRetentionPeriod", "1209600" }
            }
        };

        _sqsClientMock.Setup(x => x.GetQueueAttributesAsync(It.IsAny<GetQueueAttributesRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _messageQueue.GetQueueAttributesAsync(queueUrl, attributeNames);

        // Assert
        result.ShouldBe(expectedResponse);
        _sqsClientMock.Verify(x => x.GetQueueAttributesAsync(
            It.Is<GetQueueAttributesRequest>(r =>
                r.QueueUrl == queueUrl &&
                r.AttributeNames.SequenceEqual(attributeNames)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateQueueAsync_ValidRequest_ReturnsQueueUrl()
    {
        // Arrange
        var queueName = "test-queue";
        var attributes = new Dictionary<string, string>
        {
            { "VisibilityTimeout", "30" },
            { "MessageRetentionPeriod", "1209600" }
        };

        var expectedResponse = new CreateQueueResponse
        {
            HttpStatusCode = HttpStatusCode.OK,
            QueueUrl = "https://sqs.us-east-1.amazonaws.com/123456789012/test-queue"
        };

        _sqsClientMock.Setup(x =>
                x.CreateQueueAsync(It.IsAny<CreateQueueRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _messageQueue.CreateQueueAsync(queueName, attributes);

        // Assert
        result.ShouldBe(expectedResponse);
        _sqsClientMock.Verify(x => x.CreateQueueAsync(
            It.Is<CreateQueueRequest>(r =>
                r.QueueName == queueName &&
                r.Attributes.Count == 2),
            It.IsAny<CancellationToken>()), Times.Once);

        VerifyLoggerInfoWasCalled(
            $"Created queue {queueName} with URL {expectedResponse.QueueUrl}");
    }

    [Fact]
    public async Task GetQueueUrlAsync_ValidQueueName_ReturnsUrl()
    {
        // Arrange
        var queueName = "test-queue";
        var expectedQueueUrl = "https://sqs.us-east-1.amazonaws.com/123456789012/test-queue";

        var expectedResponse = new GetQueueUrlResponse
        {
            HttpStatusCode = HttpStatusCode.OK,
            QueueUrl = expectedQueueUrl
        };

        _sqsClientMock.Setup(x =>
                x.GetQueueUrlAsync(It.IsAny<GetQueueUrlRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _messageQueue.GetQueueUrlAsync(queueName);

        // Assert
        result.ShouldBe(expectedQueueUrl);
        _sqsClientMock.Verify(x => x.GetQueueUrlAsync(
            It.Is<GetQueueUrlRequest>(r => r.QueueName == queueName),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private void VerifyLoggerDebugWasCalled(string expectedMessage)
    {
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private void VerifyLoggerWarningWasCalled(string expectedMessage)
    {
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
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
            Times.Once);
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
            Times.Once);
    }
}

public class TestMessage
{
    public string Id { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}