using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ThirdOpinion.Common.Aws.DynamoDb;

//using DocNlpService.Repositories;

namespace ThirdOpinion.Common.Aws.SQS;

public class SqsMessageHandler : BackgroundService
{
    private readonly IDynamoDbRepository _dynamoRepository;
    private readonly ILogger<SqsMessageHandler> _logger;
    private readonly string _queueUrl;
    private readonly IServiceProvider _serviceProvider;
    private readonly IAmazonSQS _sqsClient;

    public SqsMessageHandler(
        IAmazonSQS sqsClient,
        ILogger<SqsMessageHandler> logger,
        IConfiguration configuration,
        IServiceProvider serviceProvider,
        IDynamoDbRepository dynamoRepository)
    {
        _sqsClient = sqsClient;
        _logger = logger;
        _serviceProvider = serviceProvider;
        _dynamoRepository = dynamoRepository;
        _queueUrl = configuration["AWS:SQS:QueueUrl"]
                    ?? throw new ArgumentNullException("AWS:SQS:QueueUrl configuration is missing");
    }

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

    private async Task ProcessMessageAsync(Message message, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing message {MessageId}", message.MessageId);
        // Use the DynamoDB repository to save the message
        //  await _dynamoRepository.SaveMessageAsync(message.MessageId, message.Body);

        // Add your message processing logic here
        // You might want to deserialize the message body and handle different message types
    }

    private async Task DeleteMessageAsync(string receiptHandle, CancellationToken cancellationToken)
    {
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