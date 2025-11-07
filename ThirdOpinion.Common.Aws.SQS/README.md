# ThirdOpinion.Common.Aws.SQS

Common SQS messaging and utilities for ThirdOpinion applications.

## Features

- Simple SQS message queue service
- Support for JSON message serialization
- Message batching for efficiency
- Dead letter queue support
- Long polling support
- Automatic retry with exponential backoff
- Message attributes support
- FIFO queue support

## Installation

```bash
dotnet add package ThirdOpinion.Common.Aws.SQS
```

## Usage

```csharp
// Register in DI container
services.AddSqsMessaging(configuration);

// Use in your service
public class MyService
{
    private readonly ISqsMessageQueue _queue;
    
    public MyService(ISqsMessageQueue queue)
    {
        _queue = queue;
    }
    
    public async Task SendMessageAsync<T>(T message)
    {
        await _queue.SendMessageAsync("my-queue-url", message);
    }
    
    public async Task ProcessMessagesAsync()
    {
        await _queue.ReceiveMessagesAsync<MyMessage>("my-queue-url", async (message) =>
        {
            // Process message
            return true; // Return true to delete message
        });
    }
}
```

## Configuration

```json
{
  "SQS": {
    "Region": "us-east-2",
    "ServiceUrl": "http://localhost:4566"
  }
}
```