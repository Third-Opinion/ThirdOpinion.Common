using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.S3.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Configuration;
using ThirdOpinion.Common.FunctionalTests.Infrastructure;
using Xunit.Abstractions;
using Shouldly;
using System.Text.Json;

namespace ThirdOpinion.Common.FunctionalTests.Tests;

[Collection("CrossService")]
public class CrossServiceIntegrationTests : BaseIntegrationTest
{
    private readonly string _testPrefix;
    private string? _userPoolId;
    private string? _clientId;
    private string? _tableName;
    private string? _bucketName;
    private string? _queueUrl;
    
    public CrossServiceIntegrationTests(ITestOutputHelper output) : base(output)
    {
        _testPrefix = Configuration.GetValue<string>("TestSettings:TestResourcePrefix") ?? "functest";
    }

    protected override async Task SetupTestResourcesAsync()
    {
        await base.SetupTestResourcesAsync();
        
        // Create all AWS resources needed for cross-service testing
        await CreateCognitoResourcesAsync();
        await CreateDynamoDbResourcesAsync();
        await CreateS3ResourcesAsync();
        await CreateSqsResourcesAsync();
        
        WriteOutput("All cross-service test resources created successfully");
    }

    protected override async Task CleanupTestResourcesAsync()
    {
        var cleanupTasks = new List<Task>();

        try
        {
            // Cleanup Cognito
            if (!string.IsNullOrEmpty(_userPoolId))
            {
                cleanupTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await CognitoClient.DeleteUserPoolAsync(new DeleteUserPoolRequest { UserPoolId = _userPoolId });
                        WriteOutput($"Deleted user pool: {_userPoolId}");
                    }
                    catch (Exception ex)
                    {
                        WriteOutput($"Warning: Failed to cleanup user pool {_userPoolId}: {ex.Message}");
                    }
                }));
            }

            // Cleanup DynamoDB
            if (!string.IsNullOrEmpty(_tableName))
            {
                cleanupTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await DynamoDbClient.DeleteTableAsync(_tableName);
                        WriteOutput($"Deleted table: {_tableName}");
                    }
                    catch (Exception ex)
                    {
                        WriteOutput($"Warning: Failed to cleanup table {_tableName}: {ex.Message}");
                    }
                }));
            }

            // Cleanup S3
            if (!string.IsNullOrEmpty(_bucketName))
            {
                cleanupTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        // Delete all objects first
                        var listResponse = await S3Client.ListObjectsV2Async(new ListObjectsV2Request { BucketName = _bucketName });
                        foreach (var obj in listResponse.S3Objects)
                        {
                            await S3Client.DeleteObjectAsync(_bucketName, obj.Key);
                        }
                        
                        // Delete bucket
                        await S3Client.DeleteBucketAsync(_bucketName);
                        WriteOutput($"Deleted bucket: {_bucketName}");
                    }
                    catch (Exception ex)
                    {
                        WriteOutput($"Warning: Failed to cleanup bucket {_bucketName}: {ex.Message}");
                    }
                }));
            }

            // Cleanup SQS
            if (!string.IsNullOrEmpty(_queueUrl))
            {
                cleanupTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await SqsClient.DeleteQueueAsync(_queueUrl);
                        WriteOutput($"Deleted queue: {_queueUrl}");
                    }
                    catch (Exception ex)
                    {
                        WriteOutput($"Warning: Failed to cleanup queue {_queueUrl}: {ex.Message}");
                    }
                }));
            }

            await Task.WhenAll(cleanupTasks);
        }
        finally
        {
            await base.CleanupTestResourcesAsync();
        }
    }

    [Fact]
    public async Task UserRegistrationWorkflow_EndToEnd_ShouldSucceed()
    {
        // This test simulates a complete user registration workflow:
        // 1. Create user in Cognito
        // 2. Store user profile in DynamoDB
        // 3. Upload profile image to S3
        // 4. Send welcome notification via SQS

        // Arrange
        var (email, password, attributes) = TestDataBuilder.CreateTestUser();
        var profileData = TestDataBuilder.CreateDynamoDbTestData(email);
        var profileImageData = TestDataBuilder.CreateBinaryTestData(1024);

        // Act & Assert - Step 1: Create user in Cognito
        var createUserResponse = await CognitoClient.AdminCreateUserAsync(new AdminCreateUserRequest
        {
            UserPoolId = _userPoolId,
            Username = email,
            MessageAction = "SUPPRESS",
            TemporaryPassword = password,
            UserAttributes = attributes.Select(attr => new AttributeType
            {
                Name = attr.Key,
                Value = attr.Value
            }).ToList()
        });

        await CognitoClient.AdminSetUserPasswordAsync(new AdminSetUserPasswordRequest
        {
            UserPoolId = _userPoolId,
            Username = email,
            Password = password,
            Permanent = true
        });

        createUserResponse.User.ShouldNotBeNull();
        WriteOutput($"Step 1: Created user in Cognito: {email}");

        // Act & Assert - Step 2: Store user profile in DynamoDB
        var putItemResponse = await DynamoDbClient.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = new Dictionary<string, AttributeValue>
            {
                ["Id"] = new() { S = email },
                ["Name"] = new() { S = profileData["Name"].ToString()! },
                ["Email"] = new() { S = email },
                ["Age"] = new() { N = profileData["Age"].ToString()! },
                ["CreatedAt"] = new() { S = DateTime.UtcNow.ToString("O") },
                ["Status"] = new() { S = "Active" }
            }
        });

        putItemResponse.HttpStatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        WriteOutput($"Step 2: Stored user profile in DynamoDB for: {email}");

        // Act & Assert - Step 3: Upload profile image to S3
        var imageKey = $"profiles/{email}/avatar.jpg";
        var putObjectResponse = await S3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = imageKey,
            InputStream = new MemoryStream(profileImageData),
            ContentType = "image/jpeg",
            Metadata = 
            {
                ["user-id"] = email,
                ["upload-date"] = DateTime.UtcNow.ToString("O")
            }
        });

        putObjectResponse.HttpStatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        WriteOutput($"Step 3: Uploaded profile image to S3: {imageKey}");

        // Act & Assert - Step 4: Send welcome notification via SQS
        var welcomeMessage = new
        {
            UserId = email,
            UserName = profileData["Name"].ToString(),
            Action = "UserRegistered",
            Timestamp = DateTime.UtcNow,
            ProfileImageUrl = $"s3://{_bucketName}/{imageKey}",
            NotificationType = "Welcome"
        };

        var sendMessageResponse = await SqsClient.SendMessageAsync(new SendMessageRequest
        {
            QueueUrl = _queueUrl,
            MessageBody = JsonSerializer.Serialize(welcomeMessage),
            MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                ["UserId"] = new() { DataType = "String", StringValue = email },
                ["NotificationType"] = new() { DataType = "String", StringValue = "Welcome" }
            }
        });

        sendMessageResponse.MessageId.ShouldNotBeNullOrEmpty();
        WriteOutput($"Step 4: Sent welcome notification via SQS: {sendMessageResponse.MessageId}");

        // Final verification - Ensure all data is accessible
        var getItemResponse = await DynamoDbClient.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue> { ["Id"] = new() { S = email } }
        });
        getItemResponse.Item.ShouldNotBeEmpty();

        var getObjectResponse = await S3Client.GetObjectAsync(_bucketName, imageKey);
        getObjectResponse.ContentLength.ShouldBe(profileImageData.Length);

        var receiveMessageResponse = await SqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = _queueUrl,
            MaxNumberOfMessages = 1,
            MessageAttributeNames = new List<string> { "All" }
        });
        receiveMessageResponse.Messages.ShouldNotBeEmpty();

        WriteOutput("End-to-end user registration workflow completed successfully!");
    }

    [Fact]
    public async Task DocumentProcessingPipeline_WithAuthentication_ShouldSucceed()
    {
        // This test simulates a document processing pipeline:
        // 1. Authenticate user via Cognito
        // 2. Upload document to S3
        // 3. Create processing job record in DynamoDB
        // 4. Queue processing notification in SQS

        // Arrange
        var (email, password, attributes) = TestDataBuilder.CreateTestUser();
        var documentData = TestDataBuilder.CreateBinaryTestData(2048);
        var documentContent = "Document content for processing pipeline test";

        // Setup user
        await CognitoClient.AdminCreateUserAsync(new AdminCreateUserRequest
        {
            UserPoolId = _userPoolId,
            Username = email,
            MessageAction = "SUPPRESS",
            TemporaryPassword = password,
            UserAttributes = attributes.Select(attr => new AttributeType
            {
                Name = attr.Key,
                Value = attr.Value
            }).ToList()
        });

        await CognitoClient.AdminSetUserPasswordAsync(new AdminSetUserPasswordRequest
        {
            UserPoolId = _userPoolId,
            Username = email,
            Password = password,
            Permanent = true
        });

        // Act & Assert - Step 1: Authenticate user
        var authResponse = await CognitoClient.AdminInitiateAuthAsync(new AdminInitiateAuthRequest
        {
            UserPoolId = _userPoolId,
            ClientId = _clientId,
            AuthFlow = AuthFlowType.ADMIN_USER_PASSWORD_AUTH,
            AuthParameters = new Dictionary<string, string>
            {
                ["USERNAME"] = email,
                ["PASSWORD"] = password
            }
        });

        authResponse.AuthenticationResult.ShouldNotBeNull();
        var accessToken = authResponse.AuthenticationResult.AccessToken;
        WriteOutput($"Step 1: Authenticated user: {email}");

        // Act & Assert - Step 2: Upload document to S3
        var documentId = Guid.NewGuid().ToString();
        var documentKey = $"documents/{email}/{documentId}/document.txt";
        
        var uploadResponse = await S3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = documentKey,
            ContentBody = documentContent,
            ContentType = "text/plain",
            Metadata = 
            {
                ["user-id"] = email,
                ["document-id"] = documentId,
                ["upload-date"] = DateTime.UtcNow.ToString("O"),
                ["auth-token-hash"] = accessToken.GetHashCode().ToString()
            }
        });

        uploadResponse.HttpStatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        WriteOutput($"Step 2: Uploaded document to S3: {documentKey}");

        // Act & Assert - Step 3: Create processing job in DynamoDB
        var jobId = Guid.NewGuid().ToString();
        var putJobResponse = await DynamoDbClient.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = new Dictionary<string, AttributeValue>
            {
                ["Id"] = new() { S = jobId },
                ["UserId"] = new() { S = email },
                ["DocumentId"] = new() { S = documentId },
                ["DocumentPath"] = new() { S = documentKey },
                ["Status"] = new() { S = "Pending" },
                ["CreatedAt"] = new() { S = DateTime.UtcNow.ToString("O") },
                ["JobType"] = new() { S = "DocumentProcessing" }
            }
        });

        putJobResponse.HttpStatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        WriteOutput($"Step 3: Created processing job in DynamoDB: {jobId}");

        // Act & Assert - Step 4: Queue processing notification
        var processingMessage = new
        {
            JobId = jobId,
            UserId = email,
            DocumentId = documentId,
            DocumentPath = documentKey,
            Action = "ProcessDocument",
            Priority = "Normal",
            Timestamp = DateTime.UtcNow
        };

        var queueResponse = await SqsClient.SendMessageAsync(new SendMessageRequest
        {
            QueueUrl = _queueUrl,
            MessageBody = JsonSerializer.Serialize(processingMessage),
            MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                ["JobId"] = new() { DataType = "String", StringValue = jobId },
                ["UserId"] = new() { DataType = "String", StringValue = email },
                ["Priority"] = new() { DataType = "String", StringValue = "Normal" }
            }
        });

        queueResponse.MessageId.ShouldNotBeNullOrEmpty();
        WriteOutput($"Step 4: Queued processing notification: {queueResponse.MessageId}");

        // Simulate processing completion and update job status
        await DynamoDbClient.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue> { ["Id"] = new() { S = jobId } },
            UpdateExpression = "SET #status = :status, CompletedAt = :completedAt",
            ExpressionAttributeNames = new Dictionary<string, string> { ["#status"] = "Status" },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":status"] = new() { S = "Completed" },
                [":completedAt"] = new() { S = DateTime.UtcNow.ToString("O") }
            }
        });

        WriteOutput("Document processing pipeline completed successfully!");
    }

    [Fact]
    public async Task DataBackupAndRestore_AcrossServices_ShouldMaintainIntegrity()
    {
        // This test demonstrates data backup and restore across services:
        // 1. Store original data in DynamoDB
        // 2. Backup data to S3
        // 3. Send backup notification via SQS
        // 4. Restore data from S3 to new DynamoDB record
        // 5. Verify data integrity

        // Arrange
        var originalData = new Dictionary<string, object>
        {
            ["Id"] = Guid.NewGuid().ToString(),
            ["Name"] = "Important Business Data",
            ["Value"] = 12345.67,
            ["Active"] = true,
            ["Tags"] = new List<string> { "important", "business", "production" },
            ["CreatedAt"] = DateTime.UtcNow,
            ["Metadata"] = new Dictionary<string, object>
            {
                ["version"] = "1.0",
                ["department"] = "finance"
            }
        };

        // Act & Assert - Step 1: Store original data in DynamoDB
        var originalId = originalData["Id"].ToString()!;
        var putOriginalResponse = await DynamoDbClient.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = new Dictionary<string, AttributeValue>
            {
                ["Id"] = new() { S = originalId },
                ["Name"] = new() { S = originalData["Name"].ToString()! },
                ["Value"] = new() { N = originalData["Value"].ToString()! },
                ["Active"] = new() { BOOL = (bool)originalData["Active"] },
                ["Tags"] = new() { SS = ((List<string>)originalData["Tags"]).ToList() },
                ["CreatedAt"] = new() { S = ((DateTime)originalData["CreatedAt"]).ToString("O") },
                ["Metadata"] = new() { S = JsonSerializer.Serialize(originalData["Metadata"]) }
            }
        });

        putOriginalResponse.HttpStatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        WriteOutput($"Step 1: Stored original data in DynamoDB: {originalId}");

        // Act & Assert - Step 2: Backup data to S3
        var backupKey = $"backups/{DateTime.UtcNow:yyyy/MM/dd}/{originalId}.json";
        var backupData = JsonSerializer.Serialize(originalData, new JsonSerializerOptions { WriteIndented = true });
        
        var backupResponse = await S3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = backupKey,
            ContentBody = backupData,
            ContentType = "application/json",
            Metadata = 
            {
                ["original-id"] = originalId,
                ["backup-date"] = DateTime.UtcNow.ToString("O"),
                ["data-type"] = "business-record"
            }
        });

        backupResponse.HttpStatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        WriteOutput($"Step 2: Backed up data to S3: {backupKey}");

        // Act & Assert - Step 3: Send backup notification
        var backupNotification = new
        {
            OriginalId = originalId,
            BackupLocation = $"s3://{_bucketName}/{backupKey}",
            BackupDate = DateTime.UtcNow,
            Action = "DataBackup",
            Status = "Completed"
        };

        var notificationResponse = await SqsClient.SendMessageAsync(new SendMessageRequest
        {
            QueueUrl = _queueUrl,
            MessageBody = JsonSerializer.Serialize(backupNotification),
            MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                ["OriginalId"] = new() { DataType = "String", StringValue = originalId },
                ["Action"] = new() { DataType = "String", StringValue = "DataBackup" }
            }
        });

        notificationResponse.MessageId.ShouldNotBeNullOrEmpty();
        WriteOutput($"Step 3: Sent backup notification: {notificationResponse.MessageId}");

        // Act & Assert - Step 4: Restore data from S3
        var restoreResponse = await S3Client.GetObjectAsync(_bucketName, backupKey);
        using var reader = new StreamReader(restoreResponse.ResponseStream);
        var restoredJsonData = await reader.ReadToEndAsync();
        var restoredData = JsonSerializer.Deserialize<Dictionary<string, object>>(restoredJsonData)!;

        // Create new record with restored data
        var restoredId = Guid.NewGuid().ToString();
        var putRestoredResponse = await DynamoDbClient.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = new Dictionary<string, AttributeValue>
            {
                ["Id"] = new() { S = restoredId },
                ["Name"] = new() { S = restoredData["Name"].ToString()! },
                ["Value"] = new() { N = restoredData["Value"].ToString()! },
                ["Active"] = new() { BOOL = ((JsonElement)restoredData["Active"]).GetBoolean() },
                ["OriginalId"] = new() { S = originalId },
                ["RestoredAt"] = new() { S = DateTime.UtcNow.ToString("O") },
                ["RestoredFrom"] = new() { S = backupKey }
            }
        });

        putRestoredResponse.HttpStatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        WriteOutput($"Step 4: Restored data to DynamoDB: {restoredId}");

        // Act & Assert - Step 5: Verify data integrity
        var originalRecord = await DynamoDbClient.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue> { ["Id"] = new() { S = originalId } }
        });

        var restoredRecord = await DynamoDbClient.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue> { ["Id"] = new() { S = restoredId } }
        });

        // Verify key fields match
        originalRecord.Item["Name"].S.ShouldBe(restoredRecord.Item["Name"].S);
        originalRecord.Item["Value"].N.ShouldBe(restoredRecord.Item["Value"].N);
        originalRecord.Item["Active"].BOOL.ShouldBe(restoredRecord.Item["Active"].BOOL);
        restoredRecord.Item["OriginalId"].S.ShouldBe(originalId);

        WriteOutput("Data backup and restore across services completed with verified integrity!");
    }

    private async Task CreateCognitoResourcesAsync()
    {
        var poolName = $"{_testPrefix}-cross-service-pool";
        
        var poolResponse = await CognitoClient.CreateUserPoolAsync(new CreateUserPoolRequest
        {
            PoolName = poolName,
            Policies = new UserPoolPolicyType
            {
                PasswordPolicy = new PasswordPolicyType
                {
                    MinimumLength = 8,
                    RequireUppercase = true,
                    RequireLowercase = true,
                    RequireNumbers = true,
                    RequireSymbols = false
                }
            },
            UsernameAttributes = new List<string> { "email" },
            AutoVerifiedAttributes = new List<string> { "email" }
        });
        
        _userPoolId = poolResponse.UserPool.Id;

        var clientResponse = await CognitoClient.CreateUserPoolClientAsync(new CreateUserPoolClientRequest
        {
            UserPoolId = _userPoolId,
            ClientName = $"{poolName}-client",
            ExplicitAuthFlows = new List<string> { "ALLOW_ADMIN_USER_PASSWORD_AUTH", "ALLOW_REFRESH_TOKEN_AUTH" },
            GenerateSecret = false
        });
        
        _clientId = clientResponse.UserPoolClient.ClientId;
    }

    private async Task CreateDynamoDbResourcesAsync()
    {
        _tableName = GenerateTestResourceName("cross-service-table");
        
        await DynamoDbClient.CreateTableAsync(new CreateTableRequest
        {
            TableName = _tableName,
            KeySchema = new List<KeySchemaElement>
            {
                new() { AttributeName = "Id", KeyType = KeyType.HASH }
            },
            AttributeDefinitions = new List<AttributeDefinition>
            {
                new() { AttributeName = "Id", AttributeType = ScalarAttributeType.S }
            },
            BillingMode = BillingMode.PAY_PER_REQUEST
        });

        // Wait for table to be active
        await WaitForTableActiveAsync(_tableName);
    }

    private async Task CreateS3ResourcesAsync()
    {
        _bucketName = GenerateTestResourceName("cross-service-bucket").ToLowerInvariant();
        
        await S3Client.PutBucketAsync(new PutBucketRequest
        {
            BucketName = _bucketName
        });

        // Wait for bucket to be available
        await Task.Delay(2000);
    }

    private async Task CreateSqsResourcesAsync()
    {
        var queueName = GenerateTestResourceName("cross-service-queue");
        
        var response = await SqsClient.CreateQueueAsync(new CreateQueueRequest
        {
            QueueName = queueName,
            Attributes = new Dictionary<string, string>
            {
                [QueueAttributeName.VisibilityTimeout] = "30",
                [QueueAttributeName.MessageRetentionPeriod] = "1209600"
            }
        });
        
        _queueUrl = response.QueueUrl;

        // Wait for queue to be available
        await Task.Delay(1000);
    }

    private async Task WaitForTableActiveAsync(string tableName)
    {
        var timeout = TimeSpan.FromMinutes(2);
        var start = DateTime.UtcNow;
        
        while (DateTime.UtcNow - start < timeout)
        {
            try
            {
                var describeResponse = await DynamoDbClient.DescribeTableAsync(tableName);
                if (describeResponse.Table.TableStatus == TableStatus.ACTIVE)
                {
                    return;
                }
            }
            catch (Amazon.DynamoDBv2.Model.ResourceNotFoundException)
            {
                // Table still creating
            }
            
            await Task.Delay(1000);
        }
        
        throw new TimeoutException($"Table {tableName} did not become active within timeout");
    }
}