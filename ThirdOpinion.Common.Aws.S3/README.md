# ThirdOpinion.Common.Aws.S3

Common S3 storage and utilities for ThirdOpinion applications.

## Features

- Simple S3 storage service with async operations
- Support for multipart uploads
- Presigned URL generation
- Stream-based operations for large files
- Automatic retry with exponential backoff
- Logging integration

## Installation

```bash
dotnet add package ThirdOpinion.Common.Aws.S3
```

## Usage

```csharp
// Register in DI container
services.AddS3Storage(configuration);

// Use in your service
public class MyService
{
    private readonly IS3Storage _storage;
    
    public MyService(IS3Storage storage)
    {
        _storage = storage;
    }
    
    public async Task SaveFileAsync(Stream content, string key)
    {
        await _storage.PutObjectAsync("my-bucket", key, content);
    }
}
```

## Configuration

```json
{
  "S3": {
    "Region": "us-east-1",
    "ServiceUrl": "http://localhost:4566"
  }
}
```