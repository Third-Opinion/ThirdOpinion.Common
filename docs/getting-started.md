# Getting Started with ThirdOpinion.Common

## Overview

ThirdOpinion.Common is a comprehensive .NET library that provides robust AWS service integration and utility functions. This library simplifies working with AWS services including S3, DynamoDB, SQS, and Cognito while maintaining best practices for cloud-native applications.

## Installation

Install the ThirdOpinion.Common NuGet package:

```bash
dotnet add package ThirdOpinion.Common
```

Or via Package Manager Console:

```powershell
Install-Package ThirdOpinion.Common
```

## Prerequisites

- .NET 8.0 or later
- AWS credentials configured (via AWS CLI, environment variables, or IAM roles)
- Required AWS service permissions for the services you intend to use

## Configuration

### Basic Setup

Add AWS configuration to your `appsettings.json`:

```json
{
  "AWS": {
    "Region": "us-east-1",
    "Profile": "default" // Optional: specify AWS profile
  },
  "ThirdOpinion": {
    "S3": {
      "DefaultBucket": "your-bucket-name"
    },
    "DynamoDB": {
      "TablePrefix": "prod-"
    },
    "Cognito": {
      "UserPoolId": "us-east-1_xxxxx",
      "ClientId": "your-client-id"
    }
  }
}
```

### Dependency Injection

Register services in your `Program.cs`:

```csharp
using ThirdOpinion.Common.Aws.S3;
using ThirdOpinion.Common.Aws.DynamoDb;
using ThirdOpinion.Common.Aws.Cognito;
using ThirdOpinion.Common.Aws.SQS;

var builder = WebApplication.CreateBuilder(args);

// Add AWS services
builder.Services.AddDefaultAWSOptions(builder.Configuration.GetAWSOptions());

// Add ThirdOpinion AWS services
builder.Services.AddSingleton<IS3Service, S3Service>();
builder.Services.AddSingleton<IDynamoDbService, DynamoDbService>();
builder.Services.AddSingleton<ICognitoService, CognitoService>();
builder.Services.AddSingleton<ISQSService, SQSService>();

// Configure specific AWS clients if needed
builder.Services.AddAWSService<IAmazonS3>();
builder.Services.AddAWSService<IAmazonDynamoDB>();
builder.Services.AddAWSService<IAmazonCognitoIdentityProvider>();
builder.Services.AddAWSService<IAmazonSQS>();

var app = builder.Build();
```

## Quick Start Examples

### S3 Operations

```csharp
public class FileController : ControllerBase
{
    private readonly IS3Service _s3Service;
    
    public FileController(IS3Service s3Service)
    {
        _s3Service = s3Service;
    }
    
    [HttpPost("upload")]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        using var stream = file.OpenReadStream();
        var key = $"uploads/{Guid.NewGuid()}/{file.FileName}";
        
        await _s3Service.UploadFileAsync("my-bucket", key, stream);
        return Ok(new { key });
    }
    
    [HttpGet("download/{key}")]
    public async Task<IActionResult> Download(string key)
    {
        var stream = await _s3Service.GetFileStreamAsync("my-bucket", key);
        return File(stream, "application/octet-stream");
    }
}
```

### DynamoDB Operations

```csharp
public class UserRepository
{
    private readonly IDynamoDbService _dynamoDb;
    private const string TableName = "Users";
    
    public UserRepository(IDynamoDbService dynamoDb)
    {
        _dynamoDb = dynamoDb;
    }
    
    public async Task<User> GetUserAsync(string userId)
    {
        return await _dynamoDb.GetItemAsync<User>(
            TableName,
            new Dictionary<string, AttributeValue>
            {
                ["UserId"] = new AttributeValue { S = userId }
            });
    }
    
    public async Task SaveUserAsync(User user)
    {
        await _dynamoDb.PutItemAsync(TableName, user);
    }
}
```

### Cognito Authentication

```csharp
public class AuthService
{
    private readonly ICognitoService _cognito;
    
    public AuthService(ICognitoService cognito)
    {
        _cognito = cognito;
    }
    
    public async Task<AuthenticationResult> LoginAsync(string username, string password)
    {
        var result = await _cognito.InitiateAuthAsync(
            username,
            password,
            "your-client-id");
            
        return new AuthenticationResult
        {
            AccessToken = result.AuthenticationResult.AccessToken,
            RefreshToken = result.AuthenticationResult.RefreshToken,
            ExpiresIn = result.AuthenticationResult.ExpiresIn
        };
    }
    
    public async Task<SignUpResponse> RegisterAsync(string email, string password)
    {
        return await _cognito.SignUpAsync(
            email,
            password,
            "your-client-id",
            new Dictionary<string, string>
            {
                ["email"] = email
            });
    }
}
```

### SQS Messaging

```csharp
public class MessageProcessor
{
    private readonly ISQSService _sqs;
    private const string QueueUrl = "https://sqs.us-east-1.amazonaws.com/123456789/my-queue";
    
    public MessageProcessor(ISQSService sqs)
    {
        _sqs = sqs;
    }
    
    public async Task SendMessageAsync<T>(T message)
    {
        await _sqs.SendMessageAsync(QueueUrl, JsonSerializer.Serialize(message));
    }
    
    public async Task ProcessMessagesAsync()
    {
        var messages = await _sqs.ReceiveMessagesAsync(QueueUrl, maxMessages: 10);
        
        foreach (var message in messages)
        {
            try
            {
                // Process message
                await ProcessMessage(message.Body);
                
                // Delete message after successful processing
                await _sqs.DeleteMessageAsync(QueueUrl, message.ReceiptHandle);
            }
            catch (Exception ex)
            {
                // Log error - message will become visible again after visibility timeout
                Console.WriteLine($"Error processing message: {ex.Message}");
            }
        }
    }
}
```

## Environment Variables

The library supports configuration through environment variables:

- `AWS_REGION`: AWS region (e.g., "us-east-1")
- `AWS_ACCESS_KEY_ID`: AWS access key
- `AWS_SECRET_ACCESS_KEY`: AWS secret key
- `AWS_SESSION_TOKEN`: AWS session token (for temporary credentials)
- `AWS_PROFILE`: AWS profile name (when using AWS CLI profiles)

## Next Steps

- [S3 Service Patterns](aws-services/s3-patterns.md) - Advanced S3 operations and patterns
- [DynamoDB Patterns](aws-services/dynamodb-patterns.md) - DynamoDB best practices and patterns
- [Cognito Patterns](aws-services/cognito-patterns.md) - Authentication and authorization patterns
- [Troubleshooting Guide](troubleshooting.md) - Common issues and solutions

## Support

For issues, feature requests, or questions, please visit our [GitHub repository](https://github.com/thirdopinion/ThirdOpinion.Common).