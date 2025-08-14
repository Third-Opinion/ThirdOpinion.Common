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

    public async Task<SendMessageResponse> SendMessageAsync(string queueUrl,
        string messageBody,
        Dictionary<string, MessageAttributeValue>? messageAttributes = null,
        int? delaySeconds = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new SendMessageRequest
            {
                QueueUrl = queueUrl,
                MessageBody = messageBody
            };

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
                AttributeNames = new List<string> { "All" },
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

            if (response.Failed.Count > 0)
                _logger.LogWarning("Failed to delete {Count} messages from queue {QueueUrl}",
                    response.Failed.Count, queueUrl);

            _logger.LogDebug("Deleted batch of {Count} messages from queue {QueueUrl}",
                response.Successful.Count, queueUrl);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting message batch from queue {QueueUrl}", queueUrl);
            throw;
        }
    }

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

    public async Task<CreateQueueResponse> CreateQueueAsync(string queueName,
        Dictionary<string, string>? attributes = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new CreateQueueRequest
            {
                QueueName = queueName
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