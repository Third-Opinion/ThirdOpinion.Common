# ThirdOpinion.Common

A comprehensive .NET library providing common utilities and AWS service integrations for ThirdOpinion applications.

## Features

### AWS Service Integration

- **Amazon S3**: File storage and retrieval utilities
- **Amazon DynamoDB**: Repository patterns and type converters
- **Amazon SQS**: Message queue management and handlers
- **Amazon Cognito**: Authentication and authorization utilities

### Utilities

- String extensions and manipulations
- Patient matching algorithms
- Common data models and helpers

## Installation

```bash
dotnet add package ThirdOpinion.Common
```

## Usage

### AWS Services

Configure AWS services in your `appsettings.json`:

```json
{
  "AWS": {
    "Region": "us-east-1"
  }
}
```

Register services in your DI container:

```csharp
services.AddAws();
services.AddDynamoDb();
services.AddS3Storage();
services.AddSqsMessageQueue();
services.AddCognito();
```

### Examples

#### S3 Storage

```csharp
public class FileService
{
    private readonly IS3StorageService _s3Service;

    public FileService(IS3StorageService s3Service)
    {
        _s3Service = s3Service;
    }

    public async Task UploadFileAsync(string bucketName, string key, Stream content)
    {
        await _s3Service.UploadFileAsync(bucketName, key, content);
    }
}
```

#### DynamoDB Repository

```csharp
public class UserRepository : IDynamoDbRepository<User>
{
    private readonly IDynamoDbRepository<User> _repository;

    public UserRepository(IDynamoDbRepository<User> repository)
    {
        _repository = repository;
    }

    public async Task<User> GetUserAsync(string userId)
    {
        return await _repository.GetAsync(userId);
    }
}
```

#### SQS Message Queue

```csharp
public class NotificationService
{
    private readonly ISqsMessageQueue _messageQueue;

    public NotificationService(ISqsMessageQueue messageQueue)
    {
        _messageQueue = messageQueue;
    }

    public async Task SendNotificationAsync<T>(string queueUrl, T message)
    {
        await _messageQueue.SendMessageAsync(queueUrl, message);
    }
}
```

## Requirements

- .NET 8.0 or later
- AWS credentials configured (via AWS CLI, environment variables, or IAM roles)

## License

MIT License

## Contributing

Please refer to the project's contribution guidelines.