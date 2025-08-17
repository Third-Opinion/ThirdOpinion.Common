using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Configuration;
using ThirdOpinion.Common.FunctionalTests.Infrastructure;
using Xunit.Abstractions;
using Shouldly;
using System.Text.Json;

namespace ThirdOpinion.Common.FunctionalTests.Tests;

[Collection("SQS")]
public class SqsFunctionalTests : BaseIntegrationTest
{
    private readonly string _testPrefix;
    private readonly List<string> _createdQueues = new();
    
    public SqsFunctionalTests(ITestOutputHelper output) : base(output)
    {
        _testPrefix = Configuration.GetValue<string>("TestSettings:TestResourcePrefix") ?? "functest";
    }

    protected override async Task CleanupTestResourcesAsync()
    {
        try
        {
            foreach (var queueUrl in _createdQueues)
            {
                try
                {
                    await SqsClient.DeleteQueueAsync(queueUrl);
                    WriteOutput($"Deleted queue: {queueUrl}");
                }
                catch (QueueDoesNotExistException)
                {
                    // Queue already deleted
                }
                catch (Exception ex)
                {
                    WriteOutput($"Warning: Failed to delete queue {queueUrl}: {ex.Message}");
                }
            }
        }
        finally
        {
            await base.CleanupTestResourcesAsync();
        }
    }

    [Fact]
    public async Task CreateQueue_WithValidName_ShouldSucceed()
    {
        // Arrange
        var queueName = GenerateTestResourceName("create-test");
        
        // Act
        var response = await SqsClient.CreateQueueAsync(new CreateQueueRequest
        {
            QueueName = queueName,
            Attributes = new Dictionary<string, string>
            {
                [QueueAttributeName.VisibilityTimeout] = "30",
                [QueueAttributeName.MessageRetentionPeriod] = "1209600", // 14 days
                [QueueAttributeName.ReceiveMessageWaitTimeSeconds] = "0"
            }
        });
        _createdQueues.Add(response.QueueUrl);

        // Assert
        response.QueueUrl.ShouldNotBeNullOrEmpty();
        response.QueueUrl.ShouldContain(queueName);

        // Verify queue exists
        var listResponse = await SqsClient.ListQueuesAsync(new ListQueuesRequest
        {
            QueueNamePrefix = queueName
        });
        listResponse.QueueUrls.ShouldContain(response.QueueUrl);
        
        WriteOutput($"Successfully created queue: {queueName} at {response.QueueUrl}");
    }

    [Fact]
    public async Task SendAndReceiveMessage_WithTextContent_ShouldSucceed()
    {
        // Arrange
        var queueUrl = await CreateTestQueueAsync("send-receive-test");
        var messageBody = "This is a test message for SQS functional testing.";
        var messageAttributes = new Dictionary<string, MessageAttributeValue>
        {
            ["TestAttribute"] = new MessageAttributeValue
            {
                DataType = "String",
                StringValue = "TestValue"
            },
            ["NumberAttribute"] = new MessageAttributeValue
            {
                DataType = "Number",
                StringValue = "42"
            }
        };

        // Act - Send message
        var sendResponse = await SqsClient.SendMessageAsync(new SendMessageRequest
        {
            QueueUrl = queueUrl,
            MessageBody = messageBody,
            MessageAttributes = messageAttributes
        });

        // Act - Receive message
        var receiveResponse = await SqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = queueUrl,
            MaxNumberOfMessages = 1,
            MessageAttributeNames = new List<string> { "All" },
            WaitTimeSeconds = 5
        });

        // Assert
        sendResponse.MessageId.ShouldNotBeNullOrEmpty();
        sendResponse.MD5OfMessageBody.ShouldNotBeNullOrEmpty();

        receiveResponse.Messages.ShouldNotBeEmpty();
        var receivedMessage = receiveResponse.Messages.First();
        
        receivedMessage.Body.ShouldBe(messageBody);
        receivedMessage.MessageId.ShouldBe(sendResponse.MessageId);
        receivedMessage.MessageAttributes.ShouldContainKey("TestAttribute");
        receivedMessage.MessageAttributes["TestAttribute"].StringValue.ShouldBe("TestValue");
        receivedMessage.MessageAttributes.ShouldContainKey("NumberAttribute");
        receivedMessage.MessageAttributes["NumberAttribute"].StringValue.ShouldBe("42");
        
        WriteOutput($"Successfully sent and received message: {sendResponse.MessageId}");
    }

    [Fact]
    public async Task SendBatchMessages_WithMultipleMessages_ShouldSucceed()
    {
        // Arrange
        var queueUrl = await CreateTestQueueAsync("batch-send-test");
        var messageCount = 10;
        var batchEntries = new List<SendMessageBatchRequestEntry>();

        for (int i = 0; i < messageCount; i++)
        {
            batchEntries.Add(new SendMessageBatchRequestEntry
            {
                Id = $"msg-{i}",
                MessageBody = $"Batch message {i}",
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    ["MessageIndex"] = new MessageAttributeValue
                    {
                        DataType = "Number",
                        StringValue = i.ToString()
                    }
                }
            });
        }

        // Act
        var sendResponse = await SqsClient.SendMessageBatchAsync(new SendMessageBatchRequest
        {
            QueueUrl = queueUrl,
            Entries = batchEntries
        });

        // Assert
        sendResponse.Successful.Count.ShouldBe(messageCount);
        (sendResponse.Failed ?? new List<Amazon.SQS.Model.BatchResultErrorEntry>()).ShouldBeEmpty();

        foreach (var entry in batchEntries)
        {
            sendResponse.Successful.ShouldContain(result => result.Id == entry.Id);
        }

        // Verify messages can be received (SQS might not return all messages in one call)
        var allReceivedMessages = new List<Amazon.SQS.Model.Message>();
        int attempts = 0;
        const int maxAttempts = 5;
        
        while (allReceivedMessages.Count < messageCount && attempts < maxAttempts)
        {
            var receiveResponse = await SqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
            {
                QueueUrl = queueUrl,
                MaxNumberOfMessages = 10,
                MessageAttributeNames = new List<string> { "All" },
                WaitTimeSeconds = 2 // Short polling to get messages faster
            });
            
            allReceivedMessages.AddRange(receiveResponse.Messages);
            attempts++;
            
            if (allReceivedMessages.Count < messageCount)
            {
                await Task.Delay(1000); // Brief delay between attempts
            }
        }

        allReceivedMessages.Count.ShouldBe(messageCount);
        
        WriteOutput($"Successfully sent batch of {messageCount} messages");
    }

    [Fact]
    public async Task DeleteMessage_AfterProcessing_ShouldRemoveFromQueue()
    {
        // Arrange
        var queueUrl = await CreateTestQueueAsync("delete-test");
        var messageBody = "Message to be deleted";

        // Send message
        var sendResponse = await SqsClient.SendMessageAsync(new SendMessageRequest
        {
            QueueUrl = queueUrl,
            MessageBody = messageBody
        });

        // Receive message
        var receiveResponse = await SqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = queueUrl,
            MaxNumberOfMessages = 1
        });

        var receivedMessage = receiveResponse.Messages.First();

        // Act - Delete message
        await SqsClient.DeleteMessageAsync(new DeleteMessageRequest
        {
            QueueUrl = queueUrl,
            ReceiptHandle = receivedMessage.ReceiptHandle
        });

        // Assert - Message should not be received again
        var secondReceiveResponse = await SqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = queueUrl,
            MaxNumberOfMessages = 1,
            WaitTimeSeconds = 5
        });

        (secondReceiveResponse.Messages ?? new List<Message>()).ShouldBeEmpty();
        
        WriteOutput($"Successfully deleted message: {receivedMessage.MessageId}");
    }

    [Fact]
    public async Task ChangeMessageVisibility_ShouldExtendProcessingTime()
    {
        // Arrange
        var queueUrl = await CreateTestQueueAsync("visibility-test");
        var messageBody = "Message with extended visibility";

        await SqsClient.SendMessageAsync(new SendMessageRequest
        {
            QueueUrl = queueUrl,
            MessageBody = messageBody
        });

        var receiveResponse = await SqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = queueUrl,
            MaxNumberOfMessages = 1
        });

        var receivedMessage = receiveResponse.Messages.First();

        // Act - Extend visibility timeout
        await SqsClient.ChangeMessageVisibilityAsync(new ChangeMessageVisibilityRequest
        {
            QueueUrl = queueUrl,
            ReceiptHandle = receivedMessage.ReceiptHandle,
            VisibilityTimeout = 60 // 60 seconds
        });

        // Assert - Message should not be immediately available
        var immediateReceiveResponse = await SqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = queueUrl,
            MaxNumberOfMessages = 1,
            WaitTimeSeconds = 2
        });

        (immediateReceiveResponse.Messages ?? new List<Message>()).ShouldBeEmpty();
        
        WriteOutput($"Successfully changed visibility timeout for message: {receivedMessage.MessageId}");
    }

    [Fact]
    public async Task GetQueueAttributes_ShouldReturnConfiguration()
    {
        // Arrange
        var queueUrl = await CreateTestQueueAsync("attributes-test");
        var expectedVisibilityTimeout = "45";
        var expectedMessageRetention = "864000"; // 10 days

        // Set queue attributes
        await SqsClient.SetQueueAttributesAsync(new SetQueueAttributesRequest
        {
            QueueUrl = queueUrl,
            Attributes = new Dictionary<string, string>
            {
                [QueueAttributeName.VisibilityTimeout] = expectedVisibilityTimeout,
                [QueueAttributeName.MessageRetentionPeriod] = expectedMessageRetention
            }
        });

        // Act
        var response = await SqsClient.GetQueueAttributesAsync(new GetQueueAttributesRequest
        {
            QueueUrl = queueUrl,
            AttributeNames = new List<string> { "All" }
        });

        // Assert
        response.Attributes.ShouldContainKey(QueueAttributeName.VisibilityTimeout);
        response.Attributes[QueueAttributeName.VisibilityTimeout].ShouldBe(expectedVisibilityTimeout);
        response.Attributes.ShouldContainKey(QueueAttributeName.MessageRetentionPeriod);
        response.Attributes[QueueAttributeName.MessageRetentionPeriod].ShouldBe(expectedMessageRetention);
        response.Attributes.ShouldContainKey(QueueAttributeName.ApproximateNumberOfMessages);
        
        WriteOutput($"Successfully retrieved queue attributes for: {queueUrl}");
    }

    [Fact]
    public async Task PurgeQueue_ShouldRemoveAllMessages()
    {
        // Arrange
        var queueUrl = await CreateTestQueueAsync("purge-test");
        var messageCount = 5;

        // Send multiple messages
        for (int i = 0; i < messageCount; i++)
        {
            await SqsClient.SendMessageAsync(new SendMessageRequest
            {
                QueueUrl = queueUrl,
                MessageBody = $"Message {i} to be purged"
            });
        }

        // Verify messages exist
        var beforePurgeResponse = await SqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = queueUrl,
            MaxNumberOfMessages = 10
        });
        beforePurgeResponse.Messages.ShouldNotBeEmpty();

        // Act
        await SqsClient.PurgeQueueAsync(new PurgeQueueRequest
        {
            QueueUrl = queueUrl
        });

        // Wait for purge to complete (can take up to 60 seconds)
        await Task.Delay(5000);

        // Assert
        var afterPurgeResponse = await SqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = queueUrl,
            MaxNumberOfMessages = 10,
            WaitTimeSeconds = 5
        });

        (afterPurgeResponse.Messages ?? new List<Message>()).ShouldBeEmpty();
        
        WriteOutput($"Successfully purged all messages from queue: {queueUrl}");
    }

    [Fact]
    public async Task LongPolling_WithWaitTime_ShouldWaitForMessages()
    {
        // Arrange
        var queueUrl = await CreateTestQueueAsync("longpoll-test");
        var messageBody = "Long polling test message";

        // Act - Start long polling (this will wait for a message)
        var pollingTask = SqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = queueUrl,
            MaxNumberOfMessages = 1,
            WaitTimeSeconds = 10 // Wait up to 10 seconds
        });

        // Send message after a delay
        await Task.Delay(2000);
        var sendTime = DateTime.UtcNow;
        await SqsClient.SendMessageAsync(new SendMessageRequest
        {
            QueueUrl = queueUrl,
            MessageBody = messageBody
        });

        var receiveResponse = await pollingTask;
        var receiveTime = DateTime.UtcNow;

        // Assert
        receiveResponse.Messages.ShouldNotBeEmpty();
        var receivedMessage = receiveResponse.Messages.First();
        receivedMessage.Body.ShouldBe(messageBody);

        // Verify that long polling worked (message was received shortly after being sent)
        var timeDifference = receiveTime - sendTime;
        timeDifference.TotalSeconds.ShouldBeLessThan(3); // Should receive within 3 seconds of sending
        
        WriteOutput($"Successfully received message via long polling after {timeDifference.TotalMilliseconds}ms");
    }

    [Fact]
    public async Task FifoQueue_WithDeduplication_ShouldMaintainOrder()
    {

        // Arrange
        var queueName = GenerateTestResourceName("fifo-test") + ".fifo";
        var queueUrl = await CreateFifoQueueAsync(queueName);
        var messageGroupId = "test-group";
        var messageCount = 5;

        // Act - Send messages with sequence
        var sentMessages = new List<string>();
        for (int i = 0; i < messageCount; i++)
        {
            var messageBody = $"FIFO message {i:D3}";
            var response = await SqsClient.SendMessageAsync(new SendMessageRequest
            {
                QueueUrl = queueUrl,
                MessageBody = messageBody,
                MessageGroupId = messageGroupId,
                MessageDeduplicationId = Guid.NewGuid().ToString()
            });
            sentMessages.Add(messageBody);
        }

        // Act - Receive messages (FIFO queues may need time for messages to be available)
        await Task.Delay(2000); // Give FIFO queue time to process messages
        
        var receivedMessages = new List<string>();
        int attempts = 0;
        const int maxAttempts = 10;
        
        while (receivedMessages.Count < messageCount && attempts < maxAttempts)
        {
            var receiveResponse = await SqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
            {
                QueueUrl = queueUrl,
                MaxNumberOfMessages = 10,
                WaitTimeSeconds = 3 // Long polling for better message retrieval
            });

            if (receiveResponse.Messages?.Any() == true)
            {
                foreach (var message in receiveResponse.Messages)
                {
                    receivedMessages.Add(message.Body);
                }
            }
            
            attempts++;
            if (receivedMessages.Count < messageCount)
            {
                await Task.Delay(1000);
            }
        }

        // Assert
        receivedMessages.Count.ShouldBe(messageCount);
        receivedMessages.ShouldBe(sentMessages); // Order should be maintained
        
        WriteOutput($"Successfully tested FIFO queue with {messageCount} ordered messages");
    }

    private async Task<string> CreateTestQueueAsync(string testName)
    {
        var queueName = GenerateTestResourceName(testName);
        
        var response = await SqsClient.CreateQueueAsync(new CreateQueueRequest
        {
            QueueName = queueName,
            Attributes = new Dictionary<string, string>
            {
                [QueueAttributeName.VisibilityTimeout] = "30",
                [QueueAttributeName.MessageRetentionPeriod] = "1209600"
            }
        });
        
        _createdQueues.Add(response.QueueUrl);
        
        // Wait for queue to be available
        await Task.Delay(1000);
        
        return response.QueueUrl;
    }

    private async Task<string> CreateFifoQueueAsync(string queueName)
    {
        var response = await SqsClient.CreateQueueAsync(new CreateQueueRequest
        {
            QueueName = queueName,
            Attributes = new Dictionary<string, string>
            {
                [QueueAttributeName.FifoQueue] = "true",
                [QueueAttributeName.ContentBasedDeduplication] = "false",
                [QueueAttributeName.VisibilityTimeout] = "30",
                [QueueAttributeName.MessageRetentionPeriod] = "1209600"
            }
        });
        
        _createdQueues.Add(response.QueueUrl);
        
        // Wait for queue to be available
        await Task.Delay(2000);
        
        return response.QueueUrl;
    }
}