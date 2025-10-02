using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Logging;

namespace ThirdOpinion.Common.Aws.SQS;

/// <summary>
///     SQS message queue service implementation
/// </summary>
public class SqsMessageQueue : ISqsMessageQueue
{
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<SqsMessageQueue> _logger;
    private readonly IAmazonSQS _sqsClient;

    /// <summary>
    /// Initializes a new instance of the SqsMessageQueue class
    /// </summary>
    /// <param name="sqsClient">The Amazon SQS client</param>
    /// <param name="logger">The logger instance</param>
    public SqsMessageQueue(IAmazonSQS sqsClient, ILogger<SqsMessageQueue> logger)
    {
        _sqsClient = sqsClient;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <summary>
    /// Sends a message to an SQS queue with automatic JSON serialization
    /// </summary>
    /// <typeparam name="T">The type of message to send</typeparam>
    /// <param name="queueUrl">The URL of the SQS queue</param>
    /// <param name="message">The message object to send</param>
    /// <param name="messageAttributes">Optional message attributes</param>
    /// <param name="delaySeconds">Optional delay in seconds before the message becomes visible</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The send message response</returns>
    public async Task<SendMessageResponse> SendMessageAsync<T>(string queueUrl,
        T message,
        Dictionary<string, MessageAttributeValue>? messageAttributes = null,
        int? delaySeconds = null,
        CancellationToken cancellationToken = default)
    {
        string messageBody = JsonSerializer.Serialize(message, _jsonOptions);
        return await SendMessageAsync(queueUrl, messageBody, messageAttributes, delaySeconds,
            cancellationToken);
    }

    /// <summary>
    /// Sends a string message to an SQS queue
    /// </summary>
    /// <param name="queueUrl">The URL of the SQS queue</param>
    /// <param name="messageBody">The message body as a string</param>
    /// <param name="messageAttributes">Optional message attributes</param>
    /// <param name="delaySeconds">Optional delay in seconds before the message becomes visible</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The send message response</returns>
    public async Task<SendMessageResponse> SendMessageAsync(string queueUrl,
        string messageBody,
        Dictionary<string, MessageAttributeValue>? messageAttributes = null,
        int? delaySeconds = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (_sqsClient == null)
                throw new InvalidOperationException("SQS client is not initialized");

            if (string.IsNullOrEmpty(queueUrl))
                throw new ArgumentException("Queue URL cannot be null or empty", nameof(queueUrl));

            if (string.IsNullOrEmpty(messageBody))
                throw new ArgumentException("Message body cannot be null or empty", nameof(messageBody));

            SendMessageRequest request;
            try
            {
                request = new SendMessageRequest();
                request.QueueUrl = queueUrl;
                request.MessageBody = messageBody;
                request.MessageAttributes = new Dictionary<string, MessageAttributeValue>();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to create SendMessageRequest: {ex.Message}", ex);
            }

            if (messageAttributes != null)
                foreach (KeyValuePair<string, MessageAttributeValue> attr in messageAttributes)
                    request.MessageAttributes.Add(attr.Key, attr.Value);

            if (delaySeconds.HasValue) request.DelaySeconds = delaySeconds.Value;

            SendMessageResponse? response
                = await _sqsClient.SendMessageAsync(request, cancellationToken);
            _logger.LogDebug("Sent message to queue {QueueUrl}, MessageId: {MessageId}",
                queueUrl, response.MessageId);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message to queue {QueueUrl}", queueUrl);
            throw;
        }
    }

    /// <summary>
    /// Sends multiple messages to an SQS queue in a single batch with automatic JSON serialization
    /// </summary>
    /// <typeparam name="T">The type of messages to send</typeparam>
    /// <param name="queueUrl">The URL of the SQS queue</param>
    /// <param name="messages">The collection of message objects to send</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The send message batch response</returns>
    public async Task<SendMessageBatchResponse> SendMessageBatchAsync<T>(string queueUrl,
        IEnumerable<T> messages,
        CancellationToken cancellationToken = default)
    {
        List<SendMessageBatchRequestEntry> entries = messages.Select((msg, index) =>
            new SendMessageBatchRequestEntry
            {
                Id = index.ToString(),
                MessageBody = JsonSerializer.Serialize(msg, _jsonOptions)
            }).ToList();

        return await SendMessageBatchAsync(queueUrl, entries, cancellationToken);
    }

    /// <summary>
    /// Sends multiple messages to an SQS queue in a single batch using pre-configured entries
    /// </summary>
    /// <param name="queueUrl">The URL of the SQS queue</param>
    /// <param name="entries">The collection of message batch request entries</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The send message batch response</returns>
    public async Task<SendMessageBatchResponse> SendMessageBatchAsync(string queueUrl,
        IEnumerable<SendMessageBatchRequestEntry> entries,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new SendMessageBatchRequest
            {
                QueueUrl = queueUrl,
                Entries = entries.ToList()
            };

            SendMessageBatchResponse? response
                = await _sqsClient.SendMessageBatchAsync(request, cancellationToken);

            if (response.Failed.Count > 0)
                _logger.LogWarning("Failed to send {Count} messages to queue {QueueUrl}",
                    response.Failed.Count, queueUrl);

            _logger.LogDebug("Sent batch of {Count} messages to queue {QueueUrl}",
                response.Successful.Count, queueUrl);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message batch to queue {QueueUrl}", queueUrl);
            throw;
        }
    }

    /// <summary>
    /// Receives messages from an SQS queue
    /// </summary>
    /// <param name="queueUrl">The URL of the SQS queue</param>
    /// <param name="maxMessages">Maximum number of messages to receive (1-10, default: 10)</param>
    /// <param name="waitTimeSeconds">Long polling wait time in seconds</param>
    /// <param name="visibilityTimeout">Visibility timeout for received messages in seconds</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The receive message response containing the messages</returns>
    public async Task<ReceiveMessageResponse> ReceiveMessagesAsync(string queueUrl,
        int maxMessages = 10,
        int? waitTimeSeconds = null,
        int? visibilityTimeout = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new ReceiveMessageRequest
            {
                QueueUrl = queueUrl,
                MaxNumberOfMessages = maxMessages,
                MessageSystemAttributeNames = new List<string> { "All" },
                MessageAttributeNames = new List<string> { "All" }
            };

            if (waitTimeSeconds.HasValue) request.WaitTimeSeconds = waitTimeSeconds.Value;

            if (visibilityTimeout.HasValue) request.VisibilityTimeout = visibilityTimeout.Value;

            ReceiveMessageResponse? response
                = await _sqsClient.ReceiveMessageAsync(request, cancellationToken);
            _logger.LogDebug("Received {Count} messages from queue {QueueUrl}",
                response.Messages.Count, queueUrl);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error receiving messages from queue {QueueUrl}", queueUrl);
            throw;
        }
    }

    /// <summary>
    /// Receives and processes messages from an SQS queue with automatic JSON deserialization
    /// </summary>
    /// <typeparam name="T">The type to deserialize messages to</typeparam>
    /// <param name="queueUrl">The URL of the SQS queue</param>
    /// <param name="messageHandler">Function to process each message, return true to delete the message</param>
    /// <param name="maxMessages">Maximum number of messages to receive (1-10, default: 10)</param>
    /// <param name="waitTimeSeconds">Long polling wait time in seconds</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task ReceiveMessagesAsync<T>(string queueUrl,
        Func<T, Task<bool>> messageHandler,
        int maxMessages = 10,
        int? waitTimeSeconds = null,
        CancellationToken cancellationToken = default)
    {
        ReceiveMessageResponse response = await ReceiveMessagesAsync(queueUrl, maxMessages,
            waitTimeSeconds,
            cancellationToken: cancellationToken);

        var deleteEntries = new List<DeleteMessageBatchRequestEntry>();

        foreach (Message? message in response.Messages)
            try
            {
                var deserializedMessage = JsonSerializer.Deserialize<T>(message.Body, _jsonOptions);
                if (deserializedMessage != null)
                {
                    bool shouldDelete = await messageHandler(deserializedMessage);
                    if (shouldDelete)
                        deleteEntries.Add(new DeleteMessageBatchRequestEntry
                        {
                            Id = message.MessageId,
                            ReceiptHandle = message.ReceiptHandle
                        });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message {MessageId} from queue {QueueUrl}",
                    message.MessageId, queueUrl);
            }

        if (deleteEntries.Any())
            await DeleteMessageBatchAsync(queueUrl, deleteEntries, cancellationToken);
    }

    /// <summary>
    /// Deletes a single message from an SQS queue
    /// </summary>
    /// <param name="queueUrl">The URL of the SQS queue</param>
    /// <param name="receiptHandle">The receipt handle of the message to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The delete message response</returns>
    public async Task<DeleteMessageResponse> DeleteMessageAsync(string queueUrl,
        string receiptHandle,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new DeleteMessageRequest
            {
                QueueUrl = queueUrl,
                ReceiptHandle = receiptHandle
            };

            DeleteMessageResponse? response
                = await _sqsClient.DeleteMessageAsync(request, cancellationToken);
            _logger.LogDebug("Deleted message from queue {QueueUrl}", queueUrl);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting message from queue {QueueUrl}", queueUrl);
            throw;
        }
    }

    /// <summary>
    /// Deletes multiple messages from an SQS queue in a single batch
    /// </summary>
    /// <param name="queueUrl">The URL of the SQS queue</param>
    /// <param name="entries">The collection of delete message batch request entries</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The delete message batch response</returns>
    public async Task<DeleteMessageBatchResponse> DeleteMessageBatchAsync(string queueUrl,
        IEnumerable<DeleteMessageBatchRequestEntry> entries,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new DeleteMessageBatchRequest
            {
                QueueUrl = queueUrl,
                Entries = entries.ToList()
            };

            DeleteMessageBatchResponse? response
                = await _sqsClient.DeleteMessageBatchAsync(request, cancellationToken);

            if (response.Failed?.Count > 0)
                _logger.LogWarning("Failed to delete {Count} messages from queue {QueueUrl}",
                    response.Failed.Count, queueUrl);

            _logger.LogDebug("Deleted batch of {Count} messages from queue {QueueUrl}",
                response.Successful?.Count ?? 0, queueUrl);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting message batch from queue {QueueUrl}", queueUrl);
            throw;
        }
    }

    /// <summary>
    /// Changes the visibility timeout of a message in an SQS queue
    /// </summary>
    /// <param name="queueUrl">The URL of the SQS queue</param>
    /// <param name="receiptHandle">The receipt handle of the message</param>
    /// <param name="visibilityTimeout">New visibility timeout in seconds</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The change message visibility response</returns>
    public async Task<ChangeMessageVisibilityResponse> ChangeMessageVisibilityAsync(string queueUrl,
        string receiptHandle,
        int visibilityTimeout,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new ChangeMessageVisibilityRequest
            {
                QueueUrl = queueUrl,
                ReceiptHandle = receiptHandle,
                VisibilityTimeout = visibilityTimeout
            };

            return await _sqsClient.ChangeMessageVisibilityAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing message visibility for queue {QueueUrl}",
                queueUrl);
            throw;
        }
    }

    /// <summary>
    /// Gets attributes for an SQS queue
    /// </summary>
    /// <param name="queueUrl">The URL of the SQS queue</param>
    /// <param name="attributeNames">List of attribute names to retrieve (defaults to "All")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The get queue attributes response</returns>
    public async Task<GetQueueAttributesResponse> GetQueueAttributesAsync(string queueUrl,
        List<string>? attributeNames = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new GetQueueAttributesRequest
            {
                QueueUrl = queueUrl,
                AttributeNames = attributeNames ?? new List<string> { "All" }
            };

            return await _sqsClient.GetQueueAttributesAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting queue attributes for {QueueUrl}", queueUrl);
            throw;
        }
    }

    /// <summary>
    /// Creates a new SQS queue
    /// </summary>
    /// <param name="queueName">The name of the queue to create</param>
    /// <param name="attributes">Optional queue attributes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The create queue response containing the queue URL</returns>
    public async Task<CreateQueueResponse> CreateQueueAsync(string queueName,
        Dictionary<string, string>? attributes = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new CreateQueueRequest
            {
                QueueName = queueName,
                Attributes = new Dictionary<string, string>()
            };

            if (attributes != null)
                foreach (KeyValuePair<string, string> attr in attributes)
                    request.Attributes.Add(attr.Key, attr.Value);

            CreateQueueResponse? response
                = await _sqsClient.CreateQueueAsync(request, cancellationToken);
            _logger.LogInformation("Created queue {QueueName} with URL {QueueUrl}",
                queueName, response.QueueUrl);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating queue {QueueName}", queueName);
            throw;
        }
    }

    /// <summary>
    /// Gets the URL of an existing SQS queue by name
    /// </summary>
    /// <param name="queueName">The name of the queue</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The queue URL</returns>
    public async Task<string> GetQueueUrlAsync(string queueName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new GetQueueUrlRequest
            {
                QueueName = queueName
            };

            GetQueueUrlResponse? response
                = await _sqsClient.GetQueueUrlAsync(request, cancellationToken);
            return response.QueueUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting queue URL for {QueueName}", queueName);
            throw;
        }
    }

    /// <summary>
    /// Purges all messages from an SQS queue
    /// </summary>
    /// <param name="queueUrl">The URL of the SQS queue</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The purge queue response</returns>
    public async Task<PurgeQueueResponse> PurgeQueueAsync(string queueUrl,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new PurgeQueueRequest
            {
                QueueUrl = queueUrl
            };

            PurgeQueueResponse? response
                = await _sqsClient.PurgeQueueAsync(request, cancellationToken);
            _logger.LogWarning("Purged all messages from queue {QueueUrl}", queueUrl);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error purging queue {QueueUrl}", queueUrl);
            throw;
        }
    }

    /// <summary>
    /// Lists all SQS queues or queues matching a prefix
    /// </summary>
    /// <param name="queueNamePrefix">Optional prefix to filter queue names</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The list queues response containing queue URLs</returns>
    public async Task<ListQueuesResponse> ListQueuesAsync(string? queueNamePrefix = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new ListQueuesRequest();

            if (!string.IsNullOrEmpty(queueNamePrefix)) request.QueueNamePrefix = queueNamePrefix;

            return await _sqsClient.ListQueuesAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing queues with prefix {Prefix}", queueNamePrefix);
            throw;
        }
    }

    /// <summary>
    /// Adds permissions to an SQS queue for specified AWS accounts
    /// </summary>
    /// <param name="queueUrl">The URL of the SQS queue</param>
    /// <param name="label">A label for the permission</param>
    /// <param name="awsAccountIds">List of AWS account IDs to grant permissions to</param>
    /// <param name="actions">List of SQS actions to allow</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The add permission response</returns>
    public async Task<AddPermissionResponse> AddPermissionAsync(string queueUrl,
        string label,
        List<string> awsAccountIds,
        List<string> actions,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new AddPermissionRequest
            {
                QueueUrl = queueUrl,
                Label = label,
                AWSAccountIds = awsAccountIds,
                Actions = actions
            };

            return await _sqsClient.AddPermissionAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding permission to queue {QueueUrl}", queueUrl);
            throw;
        }
    }

    /// <summary>
    /// Removes permissions from an SQS queue
    /// </summary>
    /// <param name="queueUrl">The URL of the SQS queue</param>
    /// <param name="label">The label of the permission to remove</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The remove permission response</returns>
    public async Task<RemovePermissionResponse> RemovePermissionAsync(string queueUrl,
        string label,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new RemovePermissionRequest
            {
                QueueUrl = queueUrl,
                Label = label
            };

            return await _sqsClient.RemovePermissionAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing permission from queue {QueueUrl}", queueUrl);
            throw;
        }
    }

    /// <summary>
    /// Adds tags to an SQS queue
    /// </summary>
    /// <param name="queueUrl">The URL of the SQS queue</param>
    /// <param name="tags">Dictionary of tag key-value pairs</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The tag queue response</returns>
    public async Task<TagQueueResponse> TagQueueAsync(string queueUrl,
        Dictionary<string, string> tags,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new TagQueueRequest
            {
                QueueUrl = queueUrl,
                Tags = tags
            };

            return await _sqsClient.TagQueueAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tagging queue {QueueUrl}", queueUrl);
            throw;
        }
    }

    /// <summary>
    /// Lists all tags for an SQS queue
    /// </summary>
    /// <param name="queueUrl">The URL of the SQS queue</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The list queue tags response containing the tags</returns>
    public async Task<ListQueueTagsResponse> ListQueueTagsAsync(string queueUrl,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new ListQueueTagsRequest
            {
                QueueUrl = queueUrl
            };

            return await _sqsClient.ListQueueTagsAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing tags for queue {QueueUrl}", queueUrl);
            throw;
        }
    }
}