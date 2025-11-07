using System.Diagnostics;
using System.Text.Json;
using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
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
    private readonly IConfiguration _configuration;
    private readonly IAmazonDynamoDB _dynamoDbClient;
    private readonly IDynamoDbRepository _dynamoDbRepository;
    private readonly ILogger<AwsTestController> _logger;
    private readonly IAmazonS3 _s3Client;
    private readonly IS3Storage _s3Storage;
    private readonly IAmazonSQS _sqsClient;
    private readonly ISqsMessageQueue _sqsMessageQueue;

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
        string testBucket = _configuration["AWS:S3:TestBucket"] ?? "test-bucket-" + Guid.NewGuid();

        // Test 1: Create bucket
        TestResult createBucketTest = await RunTest("Create Bucket", async () =>
        {
            await _s3Storage.CreateBucketIfNotExistsAsync(testBucket);
            return new { BucketName = testBucket };
        });
        results.Results.Add(createBucketTest);

        // Test 2: Put object
        var testKey = $"test-object-{Guid.NewGuid()}.txt";
        var testContent = "This is a test object content";
        TestResult putObjectTest = await RunTest("Put Object", async () =>
        {
            PutObjectResponse response
                = await _s3Storage.PutObjectAsync(testBucket, testKey, testContent, "text/plain");
            return new { Key = testKey, response.ETag };
        });
        results.Results.Add(putObjectTest);

        // Test 3: Get object
        TestResult getObjectTest = await RunTest("Get Object", async () =>
        {
            string content = await _s3Storage.GetObjectAsStringAsync(testBucket, testKey);
            if (content != testContent)
                throw new Exception($"Content mismatch. Expected: {testContent}, Got: {content}");
            return new { Content = content };
        });
        results.Results.Add(getObjectTest);

        // Test 4: List objects
        TestResult listObjectsTest = await RunTest("List Objects", async () =>
        {
            IEnumerable<S3Object> objects = await _s3Storage.ListObjectsAsync(testBucket);
            int count = objects.Count();
            if (count == 0)
                throw new Exception("No objects found");
            return new { ObjectCount = count };
        });
        results.Results.Add(listObjectsTest);

        // Test 5: Delete object
        TestResult deleteObjectTest = await RunTest("Delete Object", async () =>
        {
            await _s3Storage.DeleteObjectAsync(testBucket, testKey);
            bool exists = await _s3Storage.ObjectExistsAsync(testBucket, testKey);
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
        string testTableName
            = _configuration["AWS:DynamoDB:TestTable"] ?? "test-table-" + Guid.NewGuid();

        // Test 1: Create table
        TestResult createTableTest = await RunTest("Create Table", async () =>
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
            await WaitForTableToBeActiveAsync(testTableName);

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

        TestResult saveItemTest = await RunTest("Save Item", async () =>
        {
            var config = new DynamoDBOperationConfig
            {
                OverrideTableName = testTableName
            };
            await _dynamoDbRepository.SaveAsync(testItem, config);
            return new { ItemId = testItem.Id };
        });
        results.Results.Add(saveItemTest);

        // Test 3: Load item
        TestResult loadItemTest = await RunTest("Load Item", async () =>
        {
            var config = new DynamoDBOperationConfig
            {
                OverrideTableName = testTableName
            };
            var item = await _dynamoDbRepository.LoadAsync<TestDynamoItem>(testItem.Id, null,
                config);
            if (item == null)
                throw new Exception("Item not found");
            if (item.Name != testItem.Name)
                throw new Exception($"Name mismatch. Expected: {testItem.Name}, Got: {item.Name}");
            return new { Item = item };
        });
        results.Results.Add(loadItemTest);

        // Test 4: Delete item
        TestResult deleteItemTest = await RunTest("Delete Item", async () =>
        {
            var config = new DynamoDBOperationConfig
            {
                OverrideTableName = testTableName
            };
            await _dynamoDbRepository.DeleteAsync<TestDynamoItem>(testItem.Id, null, config);
            var item = await _dynamoDbRepository.LoadAsync<TestDynamoItem>(testItem.Id, null,
                config);
            if (item != null)
                throw new Exception("Item still exists after deletion");
            return new { Deleted = true };
        });
        results.Results.Add(deleteItemTest);

        // Cleanup: Delete table
        try
        {
            await _dynamoDbClient.DeleteTableAsync(testTableName);
            _logger.LogDebug("Initiated deletion of test table {Table}", testTableName);
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
        string testQueueName
            = _configuration["AWS:SQS:TestQueue"] ?? "test-queue-" + Guid.NewGuid();
        string? queueUrl = null;

        // Test 1: Create queue
        TestResult createQueueTest = await RunTest("Create Queue", async () =>
        {
            CreateQueueResponse? response = await _sqsClient.CreateQueueAsync(new CreateQueueRequest
            {
                QueueName = testQueueName
            });
            queueUrl = response.QueueUrl;
            return new { QueueUrl = queueUrl };
        });
        results.Results.Add(createQueueTest);

        if (queueUrl == null) return Ok(results);

        // Test 2: Send message
        var testMessage = new TestMessage
        {
            Id = Guid.NewGuid().ToString(),
            Content = "Test message content",
            Timestamp = DateTime.UtcNow
        };

        TestResult sendMessageTest = await RunTest("Send Message", async () =>
        {
            SendMessageResponse response
                = await _sqsMessageQueue.SendMessageAsync(queueUrl, testMessage);
            return new { response.MessageId };
        });
        results.Results.Add(sendMessageTest);

        // Test 3: Receive message
        TestResult receiveMessageTest = await RunTest("Receive Message", async () =>
        {
            // Wait a bit for message to be available in SQS
            await Task.Delay(1000);

            ReceiveMessageResponse? response = await _sqsClient.ReceiveMessageAsync(
                new ReceiveMessageRequest
                {
                    QueueUrl = queueUrl,
                    MaxNumberOfMessages = 1,
                    WaitTimeSeconds = 10
                });

            if (response.Messages.Count == 0)
                throw new Exception("No messages received");

            Message? message = response.Messages.First();

            // Use the same JsonSerializer options as SqsMessageQueue (camelCase)
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            var receivedMessage
                = JsonSerializer.Deserialize<TestMessage>(message.Body, jsonOptions);

            // Since SQS doesn't guarantee message order, just verify we got a valid message
            if (receivedMessage?.Id == null || string.IsNullOrEmpty(receivedMessage.Id))
                throw new Exception("Invalid message received");

            // Delete the message
            await _sqsClient.DeleteMessageAsync(queueUrl, message.ReceiptHandle);

            return new { MessageId = receivedMessage?.Id };
        });
        results.Results.Add(receiveMessageTest);

        // Test 4: Send batch messages
        List<TestMessage> batchMessages = Enumerable.Range(1, 5)
            .Select(i => new TestMessage
            {
                Id = Guid.NewGuid().ToString(),
                Content = $"Batch message {i}",
                Timestamp = DateTime.UtcNow
            })
            .ToList();

        TestResult sendBatchTest = await RunTest("Send Batch Messages", async () =>
        {
            List<SendMessageBatchRequestEntry> entries = batchMessages.Select((msg, idx) =>
                new SendMessageBatchRequestEntry
                {
                    Id = idx.ToString(),
                    MessageBody = JsonSerializer.Serialize(msg)
                }).ToList();

            SendMessageBatchResponse? response = await _sqsClient.SendMessageBatchAsync(
                new SendMessageBatchRequest
                {
                    QueueUrl = queueUrl,
                    Entries = entries
                });

            if (response.Failed?.Count > 0)
                throw new Exception($"Failed to send {response.Failed.Count} messages");

            return new { SentCount = response.Successful?.Count ?? 0 };
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
        TestResult listUserPoolsTest = await RunTest("List User Pools", async () =>
        {
            ListUserPoolsResponse? response = await _cognitoClient.ListUserPoolsAsync(
                new ListUserPoolsRequest
                {
                    MaxResults = 10
                });
            return new { UserPoolCount = response.UserPools.Count };
        });
        results.Results.Add(listUserPoolsTest);

        // Test 2: Describe User Pool (if any exists)
        TestResult describePoolTest = await RunTest("Describe User Pool", async () =>
        {
            ListUserPoolsResponse? listResponse = await _cognitoClient.ListUserPoolsAsync(
                new ListUserPoolsRequest
                {
                    MaxResults = 1
                });

            if (listResponse.UserPools.Count == 0)
                return new { Message = "No user pools available to test" };

            string? poolId = listResponse.UserPools.First().Id;
            DescribeUserPoolResponse? describeResponse = await _cognitoClient.DescribeUserPoolAsync(
                new DescribeUserPoolRequest
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
            ActionResult<TestSuiteResult> result = await TestS3();
            return ("S3", ((OkObjectResult)result.Result!).Value as TestSuiteResult)!;
        }));

        tasks.Add(Task.Run(async () =>
        {
            ActionResult<TestSuiteResult> result = await TestDynamoDB();
            return ("DynamoDB", ((OkObjectResult)result.Result!).Value as TestSuiteResult)!;
        }));

        tasks.Add(Task.Run(async () =>
        {
            ActionResult<TestSuiteResult> result = await TestSQS();
            return ("SQS", ((OkObjectResult)result.Result!).Value as TestSuiteResult)!;
        }));

        tasks.Add(Task.Run(async () =>
        {
            ActionResult<TestSuiteResult> result = await TestCognito();
            return ("Cognito", ((OkObjectResult)result.Result!).Value as TestSuiteResult)!;
        }));

        (string, TestSuiteResult)[] results = await Task.WhenAll(tasks);

        foreach ((string service, TestSuiteResult result) in results) allResults[service] = result;

        return Ok(allResults);
    }

    private async Task<TestResult> RunTest(string testName, Func<Task<object>> testAction)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new TestResult { TestName = testName };

        try
        {
            object details = await testAction();
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

    private async Task WaitForTableToBeActiveAsync(string tableName, int maxWaitTimeSeconds = 60)
    {
        DateTime startTime = DateTime.UtcNow;
        TimeSpan maxWaitTime = TimeSpan.FromSeconds(maxWaitTimeSeconds);

        while (DateTime.UtcNow - startTime < maxWaitTime)
            try
            {
                DescribeTableResponse? describeResponse
                    = await _dynamoDbClient.DescribeTableAsync(tableName);
                if (describeResponse.Table.TableStatus == TableStatus.ACTIVE)
                {
                    _logger.LogDebug("Table {TableName} is now active", tableName);
                    return;
                }

                _logger.LogDebug("Table {TableName} status: {Status}, waiting...", tableName,
                    describeResponse.Table.TableStatus);
                await Task.Delay(2000); // Wait 2 seconds before checking again
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking table status for {TableName}", tableName);
                await Task.Delay(2000);
            }

        throw new TimeoutException(
            $"Table {tableName} did not become active within {maxWaitTimeSeconds} seconds");
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