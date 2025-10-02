# DynamoDB Patterns and Best Practices

## Overview

The ThirdOpinion.Common DynamoDB service provides efficient patterns for working with AWS DynamoDB, including support for single-table design, batch operations, transactions, and optimized querying.

## Core Features

- Single-table design support
- Batch read/write operations
- Transaction support
- Global Secondary Index (GSI) management
- Optimistic locking
- Automatic retry with exponential backoff
- Expression builder helpers

## Data Modeling Patterns

### 1. Single Table Design

```csharp
public class SingleTableRepository
{
    private readonly IDynamoDbService _dynamoDb;
    private const string TableName = "ApplicationData";
    
    // Entity base class for single table
    public abstract class Entity
    {
        public string PK { get; set; } // Partition Key
        public string SK { get; set; } // Sort Key
        public string EntityType { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public long Version { get; set; } // For optimistic locking
    }
    
    // User entity
    public class User : Entity
    {
        public string UserId { get; set; }
        public string Email { get; set; }
        public string Name { get; set; }
        
        public User()
        {
            EntityType = "USER";
        }
        
        public void SetKeys()
        {
            PK = $"USER#{UserId}";
            SK = $"PROFILE";
        }
    }
    
    // Order entity
    public class Order : Entity
    {
        public string OrderId { get; set; }
        public string UserId { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; }
        
        public Order()
        {
            EntityType = "ORDER";
        }
        
        public void SetKeys()
        {
            PK = $"USER#{UserId}";
            SK = $"ORDER#{OrderId}";
        }
    }
    
    // Save any entity
    public async Task SaveEntityAsync<T>(T entity) where T : Entity
    {
        entity.UpdatedAt = DateTime.UtcNow;
        
        if (entity.Version == 0)
        {
            entity.CreatedAt = DateTime.UtcNow;
            entity.Version = 1;
        }
        else
        {
            entity.Version++;
        }
        
        var request = new PutItemRequest
        {
            TableName = TableName,
            Item = entity.ToAttributeMap(),
            ConditionExpression = entity.Version == 1 
                ? "attribute_not_exists(PK)" 
                : "Version = :oldVersion",
            ExpressionAttributeValues = entity.Version > 1 
                ? new Dictionary<string, AttributeValue>
                {
                    [":oldVersion"] = new AttributeValue { N = (entity.Version - 1).ToString() }
                }
                : null
        };
        
        await _dynamoDb.PutItemAsync(request);
    }
}
```

### 2. Access Patterns with GSIs

```csharp
public class AccessPatternRepository
{
    private readonly IDynamoDbService _dynamoDb;
    
    // GSI definitions
    public class GlobalSecondaryIndexes
    {
        public const string GSI1 = "GSI1-PK-SK-Index";
        public const string GSI2 = "GSI2-PK-SK-Index";
        public const string EmailIndex = "Email-Index";
    }
    
    // Query patterns
    public async Task<List<Order>> GetUserOrdersAsync(
        string userId,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        var queryRequest = new QueryRequest
        {
            TableName = TableName,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :skPrefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new AttributeValue { S = $"USER#{userId}" },
                [":skPrefix"] = new AttributeValue { S = "ORDER#" }
            }
        };
        
        if (startDate.HasValue && endDate.HasValue)
        {
            queryRequest.FilterExpression = "CreatedAt BETWEEN :start AND :end";
            queryRequest.ExpressionAttributeValues[":start"] = 
                new AttributeValue { S = startDate.Value.ToString("O") };
            queryRequest.ExpressionAttributeValues[":end"] = 
                new AttributeValue { S = endDate.Value.ToString("O") };
        }
        
        var response = await _dynamoDb.QueryAsync(queryRequest);
        return response.Items.Select(item => item.ToOrder()).ToList();
    }
    
    // Query by email using GSI
    public async Task<User> GetUserByEmailAsync(string email)
    {
        var queryRequest = new QueryRequest
        {
            TableName = TableName,
            IndexName = GlobalSecondaryIndexes.EmailIndex,
            KeyConditionExpression = "Email = :email",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":email"] = new AttributeValue { S = email }
            }
        };
        
        var response = await _dynamoDb.QueryAsync(queryRequest);
        return response.Items.FirstOrDefault()?.ToUser();
    }
}
```

### 3. Batch Operations

```csharp
public class BatchOperations
{
    private readonly IDynamoDbService _dynamoDb;
    
    public async Task BatchWriteAsync<T>(List<T> items) where T : Entity
    {
        const int batchSize = 25; // DynamoDB limit
        
        for (int i = 0; i < items.Count; i += batchSize)
        {
            var batch = items.Skip(i).Take(batchSize).ToList();
            var writeRequests = new List<WriteRequest>();
            
            foreach (var item in batch)
            {
                item.UpdatedAt = DateTime.UtcNow;
                
                writeRequests.Add(new WriteRequest
                {
                    PutRequest = new PutRequest
                    {
                        Item = item.ToAttributeMap()
                    }
                });
            }
            
            var batchRequest = new BatchWriteItemRequest
            {
                RequestItems = new Dictionary<string, List<WriteRequest>>
                {
                    [TableName] = writeRequests
                }
            };
            
            // Handle unprocessed items
            var response = await _dynamoDb.BatchWriteItemAsync(batchRequest);
            
            while (response.UnprocessedItems.Count > 0)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100));
                
                batchRequest.RequestItems = response.UnprocessedItems;
                response = await _dynamoDb.BatchWriteItemAsync(batchRequest);
            }
        }
    }
    
    public async Task<List<T>> BatchGetAsync<T>(List<(string pk, string sk)> keys) where T : Entity, new()
    {
        const int batchSize = 100; // DynamoDB limit
        var results = new List<T>();
        
        for (int i = 0; i < keys.Count; i += batchSize)
        {
            var batch = keys.Skip(i).Take(batchSize).ToList();
            
            var keysAndAttributes = batch.Select(key => 
                new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue { S = key.pk },
                    ["SK"] = new AttributeValue { S = key.sk }
                }).ToList();
            
            var request = new BatchGetItemRequest
            {
                RequestItems = new Dictionary<string, KeysAndAttributes>
                {
                    [TableName] = new KeysAndAttributes
                    {
                        Keys = keysAndAttributes
                    }
                }
            };
            
            var response = await _dynamoDb.BatchGetItemAsync(request);
            
            if (response.Responses.TryGetValue(TableName, out var items))
            {
                results.AddRange(items.Select(item => item.ToEntity<T>()));
            }
            
            // Handle unprocessed keys
            while (response.UnprocessedKeys.Count > 0)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100));
                
                request.RequestItems = response.UnprocessedKeys;
                response = await _dynamoDb.BatchGetItemAsync(request);
                
                if (response.Responses.TryGetValue(TableName, out items))
                {
                    results.AddRange(items.Select(item => item.ToEntity<T>()));
                }
            }
        }
        
        return results;
    }
}
```

### 4. Transactions

```csharp
public class TransactionPatterns
{
    private readonly IDynamoDbService _dynamoDb;
    
    public async Task TransferFundsAsync(
        string fromAccountId,
        string toAccountId,
        decimal amount)
    {
        var transactionRequest = new TransactWriteItemsRequest
        {
            TransactItems = new List<TransactWriteItem>
            {
                // Deduct from source account
                new TransactWriteItem
                {
                    Update = new Update
                    {
                        TableName = TableName,
                        Key = new Dictionary<string, AttributeValue>
                        {
                            ["PK"] = new AttributeValue { S = $"ACCOUNT#{fromAccountId}" },
                            ["SK"] = new AttributeValue { S = "BALANCE" }
                        },
                        UpdateExpression = "SET Balance = Balance - :amount, UpdatedAt = :now",
                        ConditionExpression = "Balance >= :amount",
                        ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                        {
                            [":amount"] = new AttributeValue { N = amount.ToString() },
                            [":now"] = new AttributeValue { S = DateTime.UtcNow.ToString("O") }
                        }
                    }
                },
                // Add to destination account
                new TransactWriteItem
                {
                    Update = new Update
                    {
                        TableName = TableName,
                        Key = new Dictionary<string, AttributeValue>
                        {
                            ["PK"] = new AttributeValue { S = $"ACCOUNT#{toAccountId}" },
                            ["SK"] = new AttributeValue { S = "BALANCE" }
                        },
                        UpdateExpression = "SET Balance = Balance + :amount, UpdatedAt = :now",
                        ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                        {
                            [":amount"] = new AttributeValue { N = amount.ToString() },
                            [":now"] = new AttributeValue { S = DateTime.UtcNow.ToString("O") }
                        }
                    }
                },
                // Create transaction record
                new TransactWriteItem
                {
                    Put = new Put
                    {
                        TableName = TableName,
                        Item = new Dictionary<string, AttributeValue>
                        {
                            ["PK"] = new AttributeValue { S = $"TRANSACTION#{Guid.NewGuid()}" },
                            ["SK"] = new AttributeValue { S = $"TRANSFER" },
                            ["FromAccount"] = new AttributeValue { S = fromAccountId },
                            ["ToAccount"] = new AttributeValue { S = toAccountId },
                            ["Amount"] = new AttributeValue { N = amount.ToString() },
                            ["CreatedAt"] = new AttributeValue { S = DateTime.UtcNow.ToString("O") },
                            ["Status"] = new AttributeValue { S = "COMPLETED" }
                        }
                    }
                }
            }
        };
        
        await _dynamoDb.TransactWriteItemsAsync(transactionRequest);
    }
}
```

### 5. Pagination and Scanning

```csharp
public class PaginationPatterns
{
    private readonly IDynamoDbService _dynamoDb;
    
    public async IAsyncEnumerable<T> ScanTableAsync<T>(
        Expression<Func<T, bool>> filterExpression = null,
        int pageSize = 100) where T : Entity, new()
    {
        var request = new ScanRequest
        {
            TableName = TableName,
            Limit = pageSize
        };
        
        if (filterExpression != null)
        {
            var expression = BuildFilterExpression(filterExpression);
            request.FilterExpression = expression.Expression;
            request.ExpressionAttributeNames = expression.Names;
            request.ExpressionAttributeValues = expression.Values;
        }
        
        ScanResponse response;
        do
        {
            response = await _dynamoDb.ScanAsync(request);
            
            foreach (var item in response.Items)
            {
                yield return item.ToEntity<T>();
            }
            
            request.ExclusiveStartKey = response.LastEvaluatedKey;
            
            // Rate limiting
            await Task.Delay(TimeSpan.FromMilliseconds(50));
            
        } while (response.LastEvaluatedKey.Count > 0);
    }
    
    public async Task<PagedResult<T>> QueryPagedAsync<T>(
        string partitionKey,
        string sortKeyPrefix = null,
        string paginationToken = null,
        int pageSize = 20) where T : Entity, new()
    {
        var request = new QueryRequest
        {
            TableName = TableName,
            Limit = pageSize,
            KeyConditionExpression = "PK = :pk"
        };
        
        request.ExpressionAttributeValues = new Dictionary<string, AttributeValue>
        {
            [":pk"] = new AttributeValue { S = partitionKey }
        };
        
        if (!string.IsNullOrEmpty(sortKeyPrefix))
        {
            request.KeyConditionExpression += " AND begins_with(SK, :skPrefix)";
            request.ExpressionAttributeValues[":skPrefix"] = 
                new AttributeValue { S = sortKeyPrefix };
        }
        
        if (!string.IsNullOrEmpty(paginationToken))
        {
            request.ExclusiveStartKey = DeserializePaginationToken(paginationToken);
        }
        
        var response = await _dynamoDb.QueryAsync(request);
        
        return new PagedResult<T>
        {
            Items = response.Items.Select(item => item.ToEntity<T>()).ToList(),
            NextToken = response.LastEvaluatedKey.Count > 0 
                ? SerializePaginationToken(response.LastEvaluatedKey) 
                : null,
            HasMore = response.LastEvaluatedKey.Count > 0
        };
    }
}
```

### 6. Time Series Data

```csharp
public class TimeSeriesRepository
{
    private readonly IDynamoDbService _dynamoDb;
    
    public class Metric : Entity
    {
        public string MetricName { get; set; }
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public Dictionary<string, string> Dimensions { get; set; }
        
        public void SetKeys(string deviceId)
        {
            // Partition by device and time bucket (daily)
            PK = $"DEVICE#{deviceId}#{Timestamp:yyyy-MM-dd}";
            // Sort by timestamp
            SK = $"METRIC#{Timestamp:O}#{MetricName}";
        }
    }
    
    public async Task RecordMetricAsync(
        string deviceId,
        string metricName,
        double value,
        Dictionary<string, string> dimensions = null)
    {
        var metric = new Metric
        {
            MetricName = metricName,
            Timestamp = DateTime.UtcNow,
            Value = value,
            Dimensions = dimensions ?? new Dictionary<string, string>()
        };
        
        metric.SetKeys(deviceId);
        await SaveEntityAsync(metric);
        
        // Also update aggregates
        await UpdateHourlyAggregate(deviceId, metricName, value);
    }
    
    public async Task<List<Metric>> GetMetricsAsync(
        string deviceId,
        DateTime startTime,
        DateTime endTime,
        string metricName = null)
    {
        var results = new List<Metric>();
        
        // Query each day in the range
        for (var date = startTime.Date; date <= endTime.Date; date = date.AddDays(1))
        {
            var request = new QueryRequest
            {
                TableName = TableName,
                KeyConditionExpression = "PK = :pk AND SK BETWEEN :start AND :end",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":pk"] = new AttributeValue { S = $"DEVICE#{deviceId}#{date:yyyy-MM-dd}" },
                    [":start"] = new AttributeValue { S = $"METRIC#{startTime:O}" },
                    [":end"] = new AttributeValue { S = $"METRIC#{endTime:O}~" }
                }
            };
            
            if (!string.IsNullOrEmpty(metricName))
            {
                request.FilterExpression = "MetricName = :name";
                request.ExpressionAttributeValues[":name"] = 
                    new AttributeValue { S = metricName };
            }
            
            var response = await _dynamoDb.QueryAsync(request);
            results.AddRange(response.Items.Select(item => item.ToMetric()));
        }
        
        return results;
    }
}
```

## Performance Optimization

### 1. Write Sharding

```csharp
public class WriteSharding
{
    private readonly Random _random = new Random();
    
    public string GetShardedKey(string baseKey, int shardCount = 10)
    {
        var shard = _random.Next(0, shardCount);
        return $"{baseKey}#{shard}";
    }
    
    public async Task WriteHighVolumeDataAsync(string key, object data)
    {
        var shardedKey = GetShardedKey(key);
        
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new AttributeValue { S = shardedKey },
            ["SK"] = new AttributeValue { S = $"DATA#{DateTime.UtcNow.Ticks}" },
            ["Data"] = new AttributeValue { S = JsonSerializer.Serialize(data) }
        };
        
        await _dynamoDb.PutItemAsync(TableName, item);
    }
}
```

### 2. Caching Strategies

```csharp
public class CachedRepository
{
    private readonly IDynamoDbService _dynamoDb;
    private readonly IMemoryCache _cache;
    
    public async Task<T> GetWithCacheAsync<T>(
        string key,
        TimeSpan? cacheDuration = null) where T : Entity, new()
    {
        var cacheKey = $"dynamo:{typeof(T).Name}:{key}";
        
        if (_cache.TryGetValue<T>(cacheKey, out var cached))
        {
            return cached;
        }
        
        var item = await GetItemAsync<T>(key);
        
        if (item != null)
        {
            _cache.Set(cacheKey, item, cacheDuration ?? TimeSpan.FromMinutes(5));
        }
        
        return item;
    }
    
    public async Task InvalidateCacheAsync<T>(string key)
    {
        var cacheKey = $"dynamo:{typeof(T).Name}:{key}";
        _cache.Remove(cacheKey);
    }
}
```

## Error Handling

```csharp
public class DynamoDbErrorHandler
{
    public async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        int maxRetries = 3)
    {
        var policy = Policy
            .Handle<ProvisionedThroughputExceededException>()
            .Or<ServiceUnavailableException>()
            .Or<ThrottlingException>()
            .WaitAndRetryAsync(
                maxRetries,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        "DynamoDB retry {RetryCount} after {TimeSpan}s: {Exception}",
                        retryCount,
                        timeSpan.TotalSeconds,
                        exception.Message);
                });
        
        return await policy.ExecuteAsync(operation);
    }
}
```

## Cost Optimization

1. **Use projection expressions** to fetch only needed attributes
2. **Implement proper indexes** to avoid expensive scans
3. **Use batch operations** to reduce API calls
4. **Consider DynamoDB Accelerator (DAX)** for read-heavy workloads
5. **Implement TTL** for automatic cleanup of old data

## Testing Patterns

```csharp
[Fact]
public async Task SingleTableDesign_ShouldSupportMultipleEntities()
{
    // Arrange
    var user = new User { UserId = "user123", Email = "test@example.com" };
    user.SetKeys();
    
    var order = new Order 
    { 
        OrderId = "order456", 
        UserId = "user123", 
        Amount = 99.99m 
    };
    order.SetKeys();
    
    // Act
    await _repository.SaveEntityAsync(user);
    await _repository.SaveEntityAsync(order);
    
    var userOrders = await _repository.GetUserOrdersAsync("user123");
    
    // Assert
    Assert.Single(userOrders);
    Assert.Equal("order456", userOrders.First().OrderId);
}
```

## Related Documentation

- [Getting Started](../getting-started.md)
- [S3 Patterns](s3-patterns.md)
- [Cognito Patterns](cognito-patterns.md)
- [Troubleshooting](../troubleshooting.md)