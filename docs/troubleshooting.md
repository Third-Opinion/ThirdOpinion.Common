# Troubleshooting Guide

## Common Issues and Solutions

This guide covers common issues you might encounter when using ThirdOpinion.Common libraries and their solutions.

## AWS Connection Issues

### Issue: Unable to connect to AWS services

**Symptoms:**
- `AmazonServiceException: Unable to find credentials`
- `Unable to get IAM security credentials from EC2 Instance Metadata Service`

**Solutions:**

1. **Check AWS Credentials Configuration:**
   ```bash
   # Verify AWS CLI is configured
   aws configure list
   
   # Test credentials
   aws sts get-caller-identity
   ```

2. **Set Environment Variables:**
   ```bash
   export AWS_ACCESS_KEY_ID=your_access_key
   export AWS_SECRET_ACCESS_KEY=your_secret_key
   export AWS_REGION=us-east-2
   ```

3. **Use AWS Profile:**
   ```json
   // appsettings.json
   {
     "AWS": {
       "Profile": "your-profile-name",
       "Region": "us-east-2"
     }
   }
   ```

4. **For EC2/ECS/Lambda:**
   - Ensure IAM role is attached with proper permissions
   - Check security groups allow outbound HTTPS (443)

### Issue: Region endpoint not found

**Symptoms:**
- `AmazonClientException: No RegionEndpoint or ServiceURL configured`

**Solution:**
```csharp
// Explicitly set region in code
services.AddDefaultAWSOptions(new AWSOptions
{
    Region = RegionEndpoint.USEast1
});
```

## S3 Service Issues

### Issue: Access Denied when uploading files

**Symptoms:**
- `AmazonS3Exception: Access Denied`
- Status Code: 403

**Solutions:**

1. **Check IAM Policy:**
   ```json
   {
     "Version": "2012-10-17",
     "Statement": [{
       "Effect": "Allow",
       "Action": [
         "s3:PutObject",
         "s3:PutObjectAcl",
         "s3:GetObject",
         "s3:DeleteObject"
       ],
       "Resource": "arn:aws:s3:::your-bucket/*"
     }]
   }
   ```

2. **Check Bucket Policy:**
   - Ensure bucket policy doesn't explicitly deny access
   - Verify CORS configuration for browser uploads

3. **Check Object Ownership:**
   - Verify bucket ownership settings
   - Check if bucket has "BucketOwnerEnforced" setting

### Issue: The specified bucket does not exist

**Symptoms:**
- `AmazonS3Exception: The specified bucket does not exist`

**Solutions:**

1. **Verify bucket name and region:**
   ```csharp
   // Ensure bucket exists in the correct region
   var bucketLocation = await s3Client.GetBucketLocationAsync(bucketName);
   ```

2. **Check for typos in bucket name**

3. **Ensure bucket is in the same region as configured:**
   ```bash
   aws s3api get-bucket-location --bucket your-bucket-name
   ```

### Issue: Request timeout during large file upload

**Symptoms:**
- `TaskCanceledException: The operation was canceled`
- Upload fails for files > 100MB

**Solution:**
```csharp
// Use multipart upload for large files
public async Task UploadLargeFileAsync(string bucketName, string key, Stream fileStream)
{
    var fileTransferUtility = new TransferUtility(s3Client);
    
    var uploadRequest = new TransferUtilityUploadRequest
    {
        BucketName = bucketName,
        Key = key,
        InputStream = fileStream,
        PartSize = 6291456, // 6 MB
        ConcurrentServiceRequests = 10
    };
    
    await fileTransferUtility.UploadAsync(uploadRequest);
}
```

## DynamoDB Issues

### Issue: ProvisionedThroughputExceededException

**Symptoms:**
- `ProvisionedThroughputExceededException: The level of configured provisioned throughput for the table was exceeded`

**Solutions:**

1. **Implement exponential backoff:**
   ```csharp
   var policy = Policy
       .Handle<ProvisionedThroughputExceededException>()
       .WaitAndRetryAsync(
           3,
           retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
   
   await policy.ExecuteAsync(async () => 
       await dynamoDb.PutItemAsync(request));
   ```

2. **Increase table capacity:**
   ```bash
   aws dynamodb update-table \
     --table-name YourTable \
     --provisioned-throughput ReadCapacityUnits=10,WriteCapacityUnits=10
   ```

3. **Switch to On-Demand billing:**
   ```bash
   aws dynamodb update-table \
     --table-name YourTable \
     --billing-mode PAY_PER_REQUEST
   ```

### Issue: ValidationException on Query

**Symptoms:**
- `ValidationException: Query condition missed key schema element`

**Solution:**
```csharp
// Ensure you're querying with the partition key
var request = new QueryRequest
{
    TableName = tableName,
    KeyConditionExpression = "PK = :pk", // Must include partition key
    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
    {
        [":pk"] = new AttributeValue { S = partitionKeyValue }
    }
};
```

### Issue: Item size exceeds limit

**Symptoms:**
- `ValidationException: Item size has exceeded the maximum allowed size of 400 KB`

**Solutions:**

1. **Compress large attributes:**
   ```csharp
   var compressedData = CompressString(largeJsonData);
   item["Data"] = new AttributeValue { B = new MemoryStream(compressedData) };
   ```

2. **Store large items in S3:**
   ```csharp
   // Store metadata in DynamoDB, actual data in S3
   var s3Key = $"large-items/{itemId}";
   await s3Service.UploadJsonAsync(bucket, s3Key, largeObject);
   
   // Store reference in DynamoDB
   item["DataLocation"] = new AttributeValue { S = s3Key };
   ```

## Cognito Issues

### Issue: InvalidPasswordException

**Symptoms:**
- `InvalidPasswordException: Password does not conform to policy`

**Solution:**
```csharp
// Display password requirements to user
public class PasswordPolicy
{
    public const int MinLength = 8;
    public const bool RequireUppercase = true;
    public const bool RequireLowercase = true;
    public const bool RequireNumbers = true;
    public const bool RequireSymbols = true;
    public const string Symbols = "!@#$%^&*()_+-=[]{}|;:,.<>?";
    
    public static bool Validate(string password)
    {
        if (password.Length < MinLength) return false;
        if (RequireUppercase && !password.Any(char.IsUpper)) return false;
        if (RequireLowercase && !password.Any(char.IsLower)) return false;
        if (RequireNumbers && !password.Any(char.IsDigit)) return false;
        if (RequireSymbols && !password.Any(c => Symbols.Contains(c))) return false;
        return true;
    }
}
```

### Issue: UserNotFoundException

**Symptoms:**
- `UserNotFoundException: User does not exist`

**Solutions:**

1. **Check username format:**
   ```csharp
   // Cognito usernames are case-sensitive
   var username = email.ToLowerInvariant();
   ```

2. **Verify user pool ID:**
   ```csharp
   // Ensure you're using the correct user pool
   var userPoolId = Configuration["Cognito:UserPoolId"];
   ```

### Issue: Token expired

**Symptoms:**
- `NotAuthorizedException: Access Token has expired`

**Solution:**
```csharp
public async Task<string> GetValidTokenAsync(string refreshToken)
{
    try
    {
        // Try to use existing token
        return GetStoredAccessToken();
    }
    catch (TokenExpiredException)
    {
        // Refresh the token
        var request = new InitiateAuthRequest
        {
            ClientId = clientId,
            AuthFlow = AuthFlowType.REFRESH_TOKEN_AUTH,
            AuthParameters = new Dictionary<string, string>
            {
                ["REFRESH_TOKEN"] = refreshToken
            }
        };
        
        var response = await cognito.InitiateAuthAsync(request);
        StoreTokens(response.AuthenticationResult);
        return response.AuthenticationResult.AccessToken;
    }
}
```

## SQS Issues

### Issue: Message not being processed

**Symptoms:**
- Messages remain in queue
- No errors in logs

**Solutions:**

1. **Check visibility timeout:**
   ```csharp
   // Ensure visibility timeout > processing time
   var request = new ReceiveMessageRequest
   {
       QueueUrl = queueUrl,
       VisibilityTimeout = 300, // 5 minutes
       WaitTimeSeconds = 20     // Long polling
   };
   ```

2. **Verify message deletion:**
   ```csharp
   try
   {
       // Process message
       await ProcessMessage(message);
       
       // Always delete on success
       await sqs.DeleteMessageAsync(queueUrl, message.ReceiptHandle);
   }
   catch (Exception ex)
   {
       // Log error, message will reappear after visibility timeout
       logger.LogError(ex, "Failed to process message");
   }
   ```

### Issue: Messages being processed multiple times

**Symptoms:**
- Duplicate processing despite successful deletion

**Solution:**
```csharp
// Implement idempotency
public async Task ProcessMessageAsync(Message message)
{
    var messageId = message.MessageId;
    
    // Check if already processed
    if (await IsProcessedAsync(messageId))
    {
        logger.LogWarning($"Message {messageId} already processed");
        await sqs.DeleteMessageAsync(queueUrl, message.ReceiptHandle);
        return;
    }
    
    // Process message
    await DoWork(message);
    
    // Mark as processed
    await MarkAsProcessedAsync(messageId);
    
    // Delete from queue
    await sqs.DeleteMessageAsync(queueUrl, message.ReceiptHandle);
}
```

## Performance Issues

### Issue: Slow DynamoDB queries

**Solutions:**

1. **Use projection expressions:**
   ```csharp
   var request = new QueryRequest
   {
       TableName = tableName,
       ProjectionExpression = "Id, Title, UpdatedAt", // Only fetch needed attributes
       KeyConditionExpression = "PK = :pk"
   };
   ```

2. **Implement caching:**
   ```csharp
   services.AddMemoryCache();
   services.AddSingleton<ICachedDynamoDbService, CachedDynamoDbService>();
   ```

3. **Use batch operations:**
   ```csharp
   // Batch get instead of multiple individual gets
   var batchGet = new BatchGetItemRequest
   {
       RequestItems = new Dictionary<string, KeysAndAttributes>
       {
           [tableName] = new KeysAndAttributes { Keys = keys }
       }
   };
   ```

### Issue: High S3 API costs

**Solutions:**

1. **Enable S3 Transfer Acceleration:**
   ```csharp
   var config = new AmazonS3Config
   {
       UseAccelerateEndpoint = true
   };
   ```

2. **Use appropriate storage class:**
   ```csharp
   var request = new PutObjectRequest
   {
       BucketName = bucket,
       Key = key,
       StorageClass = S3StorageClass.IntelligentTiering
   };
   ```

3. **Implement client-side caching:**
   ```csharp
   // Cache frequently accessed objects
   public async Task<Stream> GetCachedObjectAsync(string key)
   {
       var cacheKey = $"s3:{bucket}:{key}";
       
       if (cache.TryGetValue<byte[]>(cacheKey, out var cached))
       {
           return new MemoryStream(cached);
       }
       
       var stream = await s3.GetObjectStreamAsync(bucket, key);
       // Cache small objects only
       if (stream.Length < 1_000_000) // 1MB
       {
           var bytes = stream.ToByteArray();
           cache.Set(cacheKey, bytes, TimeSpan.FromHours(1));
           return new MemoryStream(bytes);
       }
       
       return stream;
   }
   ```

## Debugging Tips

### Enable AWS SDK Logging

```csharp
// In Program.cs
builder.Logging.AddAWSProvider(builder.Configuration.GetAWSLoggingConfigSection());

// appsettings.json
{
  "AWS.Logging": {
    "Region": "us-east-2",
    "LogLevel": {
      "Default": "Debug",
      "Amazon": "Information"
    }
  }
}
```

### Use AWS X-Ray for Tracing

```csharp
services.AddAWSService<IAmazonXRay>();
services.AddSingleton<ITracingService, XRayTracingService>();

// Trace AWS SDK calls
AWSXRayRecorder.InitializeInstance(configuration);
AWSXRayRecorder.RegisterXRay();
```

### Local Testing with LocalStack

```bash
# Start LocalStack
docker run --rm -p 4566:4566 localstack/localstack

# Configure SDK for local endpoint
var s3Client = new AmazonS3Client(new AmazonS3Config
{
    ServiceURL = "http://localhost:4566",
    ForcePathStyle = true,
    UseHttp = true
});
```

## Getting Help

If you continue to experience issues:

1. **Check AWS Service Health Dashboard**: https://status.aws.amazon.com/
2. **Review CloudWatch Logs** for detailed error messages
3. **Enable SDK debug logging** (see above)
4. **Open an issue** on our [GitHub repository](https://github.com/thirdopinion/ThirdOpinion.Common/issues)
5. **Contact AWS Support** for service-specific issues

## Related Documentation

- [Getting Started](getting-started.md)
- [S3 Patterns](aws-services/s3-patterns.md)
- [DynamoDB Patterns](aws-services/dynamodb-patterns.md)
- [Cognito Patterns](aws-services/cognito-patterns.md)