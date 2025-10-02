# ThirdOpinion.Common.Aws.DynamoDb

Common DynamoDB repository and utilities for ThirdOpinion applications.

## Features

- Generic DynamoDB repository with CRUD operations
- Support for query and scan operations
- Batch operations support
- Type converters for common types (Guid, DateTime, etc.)
- Async/await pattern throughout
- Cancellation token support
- Logging integration

## Installation

```bash
dotnet add package ThirdOpinion.Common.Aws.DynamoDb
```

## Usage

```csharp
// Register in DI container
services.AddDynamoDbRepository();

// Use in your service
public class MyService
{
    private readonly IDynamoDbRepository _repository;
    
    public MyService(IDynamoDbRepository repository)
    {
        _repository = repository;
    }
    
    public async Task SaveEntityAsync(MyEntity entity)
    {
        await _repository.SaveAsync(entity);
    }
}
```

## Configuration

```json
{
  "DynamoDb": {
    "ServiceUrl": "http://localhost:8000",
    "Region": "us-east-1"
  }
}
```