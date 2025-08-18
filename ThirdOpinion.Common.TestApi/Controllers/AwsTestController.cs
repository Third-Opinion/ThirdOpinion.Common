using System.Diagnostics;
using System.Text.Json;
using Amazon.CognitoIdentityProvider;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.AspNetCore.Mvc;
using ThirdOpinion.Common.Aws.DynamoDb;
using ThirdOpinion.Common.Aws.S3;
using ThirdOpinion.Common.Aws.SQS;
using ThirdOpinion.Common.TestApi.Models;

namespace ThirdOpinion.Common.TestApi.Controllers;

[ApiController]
[Route("api/aws-test")]
public class AwsTestController : ControllerBase
{
    private readonly IAmazonCognitoIdentityProvider _cognitoClient;
    private readonly IDynamoDbRepository _dynamoDbRepository;
    private readonly IS3Storage _s3Storage;
    private readonly ISqsMessageQueue _sqsMessageQueue;
    private readonly IAmazonS3 _s3Client;
    private readonly IAmazonSQS _sqsClient;
    private readonly IAmazonDynamoDB _dynamoDbClient;
    private readonly ILogger<AwsTestController> _logger;
    private readonly IConfiguration _configuration;

    public AwsTestController(
        IAmazonCognitoIdentityProvider cognitoClient,
        IDynamoDbRepository dynamoDbRepository,
        IS3Storage s3Storage,
        ISqsMessageQueue sqsMessageQueue,
        IAmazonS3 s3Client,
        IAmazonSQS sqsClient,
        IAmazonDynamoDB dynamoDbClient,
        ILogger<AwsTestController> logger,
        IConfiguration configuration)
    {
        _cognitoClient = cognitoClient;
        _dynamoDbRepository = dynamoDbRepository;
        _s3Storage = s3Storage;
        _sqsMessageQueue = sqsMessageQueue;
        _s3Client = s3Client;
        _sqsClient = sqsClient;
        _dynamoDbClient = dynamoDbClient;
        _logger = logger;
        _configuration = configuration;
    }

    [HttpGet("s3/test")]
    public async Task<ActionResult<TestSuiteResult>> TestS3()
    {
        var results = new TestSuiteResult { ServiceName = "S3" };
        var testBucket = _configuration["AWS:S3:TestBucket"] ?? "test-bucket-" + Guid.NewGuid().ToString();

        // Test 1: Create bucket
        var createBucketTest = await RunTest("Create Bucket", async () =>
        {
            await _s3Storage.CreateBucketIfNotExistsAsync(testBucket);
            return new { BucketName = testBucket };
        });
        results.Results.Add(createBucketTest);

        // Test 2: Put object
        var testKey = $"test-object-{Guid.NewGuid()}.txt";
        var testContent = "This is a test object content";
        var putObjectTest = await RunTest("Put Object", async () =>
        {
            var response = await _s3Storage.PutObjectAsync(testBucket, testKey, testContent, "text/plain");
            return new { Key = testKey, ETag = response.ETag };
        });
        results.Results.Add(putObjectTest);

        // Test 3: Get object
        var getObjectTest = await RunTest("Get Object", async () =>
        {
            var content = await _s3Storage.GetObjectAsStringAsync(testBucket, testKey);
            if (content != testContent)
                throw new Exception($"Content mismatch. Expected: {testContent}, Got: {content}");
            return new { Content = content };
        });
        results.Results.Add(getObjectTest);

        // Test 4: List objects
        var listObjectsTest = await RunTest("List Objects", async () =>
        {
            var objects = await _s3Storage.ListObjectsAsync(testBucket);
            var count = objects.Count();
            if (count == 0)
                throw new Exception("No objects found");
            return new { ObjectCount = count };
        });
        results.Results.Add(listObjectsTest);

        // Test 5: Delete object
        var deleteObjectTest = await RunTest("Delete Object", async () =>
        {
            await _s3Storage.DeleteObjectAsync(testBucket, testKey);
            var exists = await _s3Storage.ObjectExistsAsync(testBucket, testKey);
            if (exists)
                throw new Exception("Object still exists after deletion");
            return new { Deleted = true };
        });
        results.Results.Add(deleteObjectTest);

        // Cleanup: Delete bucket
        try
        {
            await _s3Client.DeleteBucketAsync(testBucket);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup test bucket {Bucket}", testBucket);
        }

        return Ok(results);
    }

    [HttpGet("dynamodb/test")]
    public async Task<ActionResult<TestSuiteResult>> TestDynamoDB()
    {
        var results = new TestSuiteResult { ServiceName = "DynamoDB" };
        var testTableName = _configuration["AWS:DynamoDB:TestTable"] ?? "test-table-" + Guid.NewGuid().ToString();

        // Test 1: Create table
        var createTableTest = await RunTest("Create Table", async () =>
        {
            var request = new CreateTableRequest
            {
                TableName = testTableName,
                KeySchema = new List<KeySchemaElement>
                {
                    new() { AttributeName = "Id", KeyType = KeyType.HASH }
                },
                AttributeDefinitions = new List<AttributeDefinition>
                {
                    new() { AttributeName = "Id", AttributeType = ScalarAttributeType.S }
                },
                BillingMode = BillingMode.PAY_PER_REQUEST
            };

            await _dynamoDbClient.CreateTableAsync(request);
            
            // Wait for table to be active
            await Task.Delay(5000);
            
            return new { TableName = testTableName };
        });
        results.Results.Add(createTableTest);

        // Test 2: Save item
        var testItem = new TestDynamoItem
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Test Item",
            Value = 42,
            CreatedAt = DateTime.UtcNow
        };

        var saveItemTest = await RunTest("Save Item", async () =>
        {
            await _dynamoDbRepository.SaveAsync(testItem);
            return new { ItemId = testItem.Id };
        });
        results.Results.Add(saveItemTest);

        // Test 3: Load item
        var loadItemTest = await RunTest("Load Item", async () =>
        {
            var item = await _dynamoDbRepository.LoadAsync<TestDynamoItem>(testItem.Id);
            if (item == null)
                throw new Exception("Item not found");
            if (item.Name != testItem.Name)
                throw new Exception($"Name mismatch. Expected: {testItem.Name}, Got: {item.Name}");
            return new { Item = item };
        });
        results.Results.Add(loadItemTest);

        // Test 4: Delete item
        var deleteItemTest = await RunTest("Delete Item", async () =>
        {
            await _dynamoDbRepository.DeleteAsync<TestDynamoItem>(testItem.Id);
            var item = await _dynamoDbRepository.LoadAsync<TestDynamoItem>(testItem.Id);
            if (item != null)
                throw new Exception("Item still exists after deletion");
            return new { Deleted = true };
        });
        results.Results.Add(deleteItemTest);

        // Cleanup: Delete table
        try
        {
            await _dynamoDbClient.DeleteTableAsync(testTableName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup test table {Table}", testTableName);
        }

        return Ok(results);
    }

    [HttpGet("sqs/test")]
    public async Task<ActionResult<TestSuiteResult>> TestSQS()
    {
        var results = new TestSuiteResult { ServiceName = "SQS" };
        var testQueueName = _configuration["AWS:SQS:TestQueue"] ?? "test-queue-" + Guid.NewGuid().ToString();
        string? queueUrl = null;

        // Test 1: Create queue
        var createQueueTest = await RunTest("Create Queue", async () =>
        {
            var response = await _sqsClient.CreateQueueAsync(new CreateQueueRequest
            {
                QueueName = testQueueName
            });
            queueUrl = response.QueueUrl;
            return new { QueueUrl = queueUrl };
        });
        results.Results.Add(createQueueTest);

        if (queueUrl == null)
        {
            return Ok(results);
        }

        // Test 2: Send message
        var testMessage = new TestMessage
        {
            Id = Guid.NewGuid().ToString(),
            Content = "Test message content",
            Timestamp = DateTime.UtcNow
        };

        var sendMessageTest = await RunTest("Send Message", async () =>
        {
            var response = await _sqsMessageQueue.SendMessageAsync(queueUrl, testMessage);
            return new { MessageId = response.MessageId };
        });
        results.Results.Add(sendMessageTest);

        // Test 3: Receive message
        var receiveMessageTest = await RunTest("Receive Message", async () =>
        {
            var response = await _sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
            {
                QueueUrl = queueUrl,
                MaxNumberOfMessages = 1,
                WaitTimeSeconds = 5
            });

            if (response.Messages.Count == 0)
                throw new Exception("No messages received");

            var message = response.Messages.First();
            var receivedMessage = JsonSerializer.Deserialize<TestMessage>(message.Body);
            
            if (receivedMessage?.Id != testMessage.Id)
                throw new Exception("Message ID mismatch");

            // Delete the message
            await _sqsClient.DeleteMessageAsync(queueUrl, message.ReceiptHandle);

            return new { MessageId = receivedMessage?.Id };
        });
        results.Results.Add(receiveMessageTest);

        // Test 4: Send batch messages
        var batchMessages = Enumerable.Range(1, 5)
            .Select(i => new TestMessage
            {
                Id = Guid.NewGuid().ToString(),
                Content = $"Batch message {i}",
                Timestamp = DateTime.UtcNow
            })
            .ToList();

        var sendBatchTest = await RunTest("Send Batch Messages", async () =>
        {
            var entries = batchMessages.Select((msg, idx) => new SendMessageBatchRequestEntry
            {
                Id = idx.ToString(),
                MessageBody = JsonSerializer.Serialize(msg)
            }).ToList();

            var response = await _sqsClient.SendMessageBatchAsync(new SendMessageBatchRequest
            {
                QueueUrl = queueUrl,
                Entries = entries
            });

            if (response.Failed.Count > 0)
                throw new Exception($"Failed to send {response.Failed.Count} messages");

            return new { SentCount = response.Successful.Count };
        });
        results.Results.Add(sendBatchTest);

        // Cleanup: Delete queue
        try
        {
            await _sqsClient.DeleteQueueAsync(queueUrl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup test queue {Queue}", queueUrl);
        }

        return Ok(results);
    }

    [HttpGet("cognito/test")]
    public async Task<ActionResult<TestSuiteResult>> TestCognito()
    {
        var results = new TestSuiteResult { ServiceName = "Cognito" };

        // Test 1: List User Pools
        var listUserPoolsTest = await RunTest("List User Pools", async () =>
        {
            var response = await _cognitoClient.ListUserPoolsAsync(new Amazon.CognitoIdentityProvider.Model.ListUserPoolsRequest
            {
                MaxResults = 10
            });
            return new { UserPoolCount = response.UserPools.Count };
        });
        results.Results.Add(listUserPoolsTest);

        // Test 2: Describe User Pool (if any exists)
        var describePoolTest = await RunTest("Describe User Pool", async () =>
        {
            var listResponse = await _cognitoClient.ListUserPoolsAsync(new Amazon.CognitoIdentityProvider.Model.ListUserPoolsRequest
            {
                MaxResults = 1
            });

            if (listResponse.UserPools.Count == 0)
            {
                return new { Message = "No user pools available to test" };
            }

            var poolId = listResponse.UserPools.First().Id;
            var describeResponse = await _cognitoClient.DescribeUserPoolAsync(new Amazon.CognitoIdentityProvider.Model.DescribeUserPoolRequest
            {
                UserPoolId = poolId
            });

            return new
            {
                PoolId = poolId,
                PoolName = describeResponse.UserPool.Name
            };
        });
        results.Results.Add(describePoolTest);

        return Ok(results);
    }

    [HttpGet("test-all")]
    public async Task<ActionResult<Dictionary<string, TestSuiteResult>>> TestAllServices()
    {
        var allResults = new Dictionary<string, TestSuiteResult>();

        // Run all tests in parallel
        var tasks = new List<Task<(string, TestSuiteResult)>>();

        tasks.Add(Task.Run(async () =>
        {
            var result = await TestS3();
            return ("S3", ((OkObjectResult)result.Result!).Value as TestSuiteResult)!;
        }));

        tasks.Add(Task.Run(async () =>
        {
            var result = await TestDynamoDB();
            return ("DynamoDB", ((OkObjectResult)result.Result!).Value as TestSuiteResult)!;
        }));

        tasks.Add(Task.Run(async () =>
        {
            var result = await TestSQS();
            return ("SQS", ((OkObjectResult)result.Result!).Value as TestSuiteResult)!;
        }));

        tasks.Add(Task.Run(async () =>
        {
            var result = await TestCognito();
            return ("Cognito", ((OkObjectResult)result.Result!).Value as TestSuiteResult)!;
        }));

        var results = await Task.WhenAll(tasks);

        foreach (var (service, result) in results)
        {
            allResults[service] = result;
        }

        return Ok(allResults);
    }

    private async Task<TestResult> RunTest(string testName, Func<Task<object>> testAction)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new TestResult { TestName = testName };

        try
        {
            var details = await testAction();
            stopwatch.Stop();
            
            result.Success = true;
            result.Duration = stopwatch.Elapsed;
            result.Message = "Test passed successfully";
            result.Details = details as Dictionary<string, object> ?? 
                           new Dictionary<string, object> { ["Result"] = details };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            result.Success = false;
            result.Duration = stopwatch.Elapsed;
            result.Message = "Test failed";
            result.Error = ex.Message;
            
            _logger.LogError(ex, "Test {TestName} failed", testName);
        }

        return result;
    }
}

// Test models
[DynamoDBTable("TestItems")]
public class TestDynamoItem
{
    [DynamoDBHashKey]
    public string Id { get; set; } = string.Empty;
    
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TestMessage
{
    public string Id { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}