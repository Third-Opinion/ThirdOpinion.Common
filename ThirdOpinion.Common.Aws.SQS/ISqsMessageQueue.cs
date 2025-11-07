using Amazon.SQS.Model;

namespace ThirdOpinion.Common.Aws.SQS;

/// <summary>
///     Interface for SQS message queue operations
/// </summary>
public interface ISqsMessageQueue
{
    /// <summary>
    ///     Send a message to a queue
    /// </summary>
    Task<SendMessageResponse> SendMessageAsync<T>(string queueUrl,
        T message,
        Dictionary<string, MessageAttributeValue>? messageAttributes = null,
        int? delaySeconds = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Send a raw string message
    /// </summary>
    Task<SendMessageResponse> SendMessageAsync(string queueUrl,
        string messageBody,
        Dictionary<string, MessageAttributeValue>? messageAttributes = null,
        int? delaySeconds = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Send multiple messages in a batch
    /// </summary>
    Task<SendMessageBatchResponse> SendMessageBatchAsync<T>(string queueUrl,
        IEnumerable<T> messages,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Send multiple messages with individual attributes
    /// </summary>
    Task<SendMessageBatchResponse> SendMessageBatchAsync(string queueUrl,
        IEnumerable<SendMessageBatchRequestEntry> entries,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Receive messages from a queue
    /// </summary>
    Task<ReceiveMessageResponse> ReceiveMessagesAsync(string queueUrl,
        int maxMessages = 10,
        int? waitTimeSeconds = null,
        int? visibilityTimeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Receive and process messages with a handler
    /// </summary>
    Task ReceiveMessagesAsync<T>(string queueUrl,
        Func<T, Task<bool>> messageHandler,
        int maxMessages = 10,
        int? waitTimeSeconds = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Delete a message
    /// </summary>
    Task<DeleteMessageResponse> DeleteMessageAsync(string queueUrl,
        string receiptHandle,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Delete multiple messages in a batch
    /// </summary>
    Task<DeleteMessageBatchResponse> DeleteMessageBatchAsync(string queueUrl,
        IEnumerable<DeleteMessageBatchRequestEntry> entries,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Change message visibility timeout
    /// </summary>
    Task<ChangeMessageVisibilityResponse> ChangeMessageVisibilityAsync(string queueUrl,
        string receiptHandle,
        int visibilityTimeout,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get queue attributes
    /// </summary>
    Task<GetQueueAttributesResponse> GetQueueAttributesAsync(string queueUrl,
        List<string>? attributeNames = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Create a queue if it doesn't exist
    /// </summary>
    Task<CreateQueueResponse> CreateQueueAsync(string queueName,
        Dictionary<string, string>? attributes = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get queue URL by name
    /// </summary>
    Task<string> GetQueueUrlAsync(string queueName, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Purge all messages from a queue
    /// </summary>
    Task<PurgeQueueResponse> PurgeQueueAsync(string queueUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     List queues with optional prefix filter
    /// </summary>
    Task<ListQueuesResponse> ListQueuesAsync(string? queueNamePrefix = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Add permission to a queue
    /// </summary>
    Task<AddPermissionResponse> AddPermissionAsync(string queueUrl,
        string label,
        List<string> awsAccountIds,
        List<string> actions,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Remove permission from a queue
    /// </summary>
    Task<RemovePermissionResponse> RemovePermissionAsync(string queueUrl,
        string label,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Tag a queue
    /// </summary>
    Task<TagQueueResponse> TagQueueAsync(string queueUrl,
        Dictionary<string, string> tags,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     List queue tags
    /// </summary>
    Task<ListQueueTagsResponse> ListQueueTagsAsync(string queueUrl,
        CancellationToken cancellationToken = default);
}