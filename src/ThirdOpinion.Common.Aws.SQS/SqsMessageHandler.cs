using System.Runtime.CompilerServices;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ThirdOpinion.Common.Aws.DynamoDb;

[assembly: InternalsVisibleTo("ThirdOpinion.Common.Aws.UnitTests")]

//using DocNlpService.Repositories;

namespace ThirdOpinion.Common.Aws.SQS;

/// <summary>
/// Background service for handling SQS messages automatically
/// </summary>
public class SqsMessageHandler : BackgroundService
{
    private readonly IDynamoDbRepository _dynamoRepository;
    private readonly ILogger<SqsMessageHandler> _logger;
    private readonly string _queueUrl;
    private readonly IServiceProvider _serviceProvider;
    private readonly IAmazonSQS _sqsClient;

    /// <summary>
    /// Initializes a new instance of the SqsMessageHandler class
    /// </summary>
    /// <param name="sqsClient">The Amazon SQS client</param>
    /// <param name="logger">The logger instance</param>
    /// <param name="configuration">The configuration provider</param>
    /// <param name="serviceProvider">The service provider for dependency injection</param>
    /// <param name="dynamoRepository">The DynamoDB repository</param>
    public SqsMessageHandler(
        IAmazonSQS sqsClient,
        ILogger<SqsMessageHandler> logger,
        IConfiguration configuration,
        IServiceProvider serviceProvider,
        IDynamoDbRepository dynamoRepository)
    {
        _sqsClient = sqsClient ?? throw new ArgumentNullException(nameof(sqsClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _dynamoRepository = dynamoRepository ?? throw new ArgumentNullException(nameof(dynamoRepository));
        ArgumentNullException.ThrowIfNull(configuration);
        _queueUrl = configuration["AWS:SQS:QueueUrl"]
                    ?? throw new ArgumentNullException("AWS:SQS:QueueUrl configuration is missing");
    }

    /// <summary>
    /// Executes the background service to continuously poll for and process SQS messages
    /// </summary>
    /// <param name="stoppingToken">Cancellation token to stop the service</param>
    /// <returns>A task representing the asynchronous operation</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
            try
            {
                var request = new ReceiveMessageRequest
                {
                    QueueUrl = _queueUrl,
                    MaxNumberOfMessages = 10,
                    WaitTimeSeconds = 20
                };

                ReceiveMessageResponse? response
                    = await _sqsClient.ReceiveMessageAsync(request, stoppingToken);

                foreach (Message? message in response.Messages)
                    try
                    {
                        await ProcessMessageAsync(message, stoppingToken);
                        await DeleteMessageAsync(message.ReceiptHandle, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing message {MessageId}",
                            message.MessageId);
                    }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error receiving messages from SQS");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
    }

    internal Task ProcessMessageAsync(Message message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        _logger.LogInformation("Processing message {MessageId}", message.MessageId);
        // Use the DynamoDB repository to save the message
        //  await _dynamoRepository.SaveMessageAsync(message.MessageId, message.Body);

        // Add your message processing logic here
        // You might want to deserialize the message body and handle different message types

        return Task.CompletedTask;
    }

    internal async Task DeleteMessageAsync(string receiptHandle, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(receiptHandle);

        try
        {
            var deleteRequest = new DeleteMessageRequest
            {
                QueueUrl = _queueUrl,
                ReceiptHandle = receiptHandle
            };

            await _sqsClient.DeleteMessageAsync(deleteRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting message {ReceiptHandle}", receiptHandle);
            throw;
        }
    }
}