using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Logging;
using QueryFilter = ThirdOpinion.Common.Aws.DynamoDb.Filters.QueryFilter;
using ScanFilter = ThirdOpinion.Common.Aws.DynamoDb.Filters.ScanFilter;

namespace ThirdOpinion.Common.Aws.DynamoDb;

/// <summary>
///     Generic DynamoDB repository implementation
/// </summary>
public class DynamoDbRepository : IDynamoDbRepository
{
    private readonly IDynamoDBContext _context;
    private readonly IAmazonDynamoDB _dynamoDbClient;
    private readonly ILogger<DynamoDbRepository> _logger;

    public DynamoDbRepository(IDynamoDBContext context, IAmazonDynamoDB dynamoDbClient, ILogger<DynamoDbRepository> logger)
    {
        _context = context;
        _dynamoDbClient = dynamoDbClient;
        _logger = logger;
    }

    public async Task SaveAsync<T>(T entity, CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.SaveAsync(entity, cancellationToken);
            _logger.LogDebug("Saved entity of type {EntityType} to DynamoDB", typeof(T).Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving entity of type {EntityType} to DynamoDB",
                typeof(T).Name);
            throw;
        }
    }

    public async Task BatchSaveAsync<T>(IEnumerable<T> entities,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var batch = _context.CreateBatchWrite<T>();
            foreach (T entity in entities) batch.AddPutItem(entity);
            await batch.ExecuteAsync(cancellationToken);
            _logger.LogDebug("Batch saved {Count} entities of type {EntityType} to DynamoDB",
                entities.Count(), typeof(T).Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error batch saving entities of type {EntityType} to DynamoDB",
                typeof(T).Name);
            throw;
        }
    }

    public async Task<T?> LoadAsync<T>(object hashKey,
        object? rangeKey = null,
        CancellationToken cancellationToken = default)
        where T : class
    {
        try
        {
            if (rangeKey == null) return await _context.LoadAsync<T>(hashKey, cancellationToken);
            return await _context.LoadAsync<T>(hashKey, rangeKey, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error loading entity of type {EntityType} with key {HashKey}/{RangeKey}",
                typeof(T).Name, hashKey, rangeKey);
            throw;
        }
    }

    public async Task DeleteAsync<T>(object hashKey,
        object? rangeKey = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (rangeKey == null)
                await _context.DeleteAsync<T>(hashKey, cancellationToken);
            else
                await _context.DeleteAsync<T>(hashKey, rangeKey, cancellationToken);
            _logger.LogDebug("Deleted entity of type {EntityType} with key {HashKey}/{RangeKey}",
                typeof(T).Name, hashKey, rangeKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error deleting entity of type {EntityType} with key {HashKey}/{RangeKey}",
                typeof(T).Name, hashKey, rangeKey);
            throw;
        }
    }

    public async Task<IEnumerable<T>> QueryAsync<T>(object hashKey,
        QueryFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            AsyncSearch<T> query;

            if (filter != null)
            {
                var conditions = new List<ScanCondition>();
                foreach (ScanCondition condition in filter.ToConditions())
                    conditions.Add(condition);
                var q = _context.QueryAsync<T>(hashKey, QueryOperator.BeginsWith, conditions);
                query = (AsyncSearch<T>)q;
            }
            else
            {
                var q = _context.QueryAsync<T>(hashKey);
                query = (AsyncSearch<T>)q;
            }

            var results = new List<T>();
            List<T>? search = await query.GetRemainingAsync(cancellationToken);
            results.AddRange(search);

            _logger.LogDebug("Query returned {Count} entities of type {EntityType}", results.Count,
                typeof(T).Name);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error querying entities of type {EntityType} with hash key {HashKey}",
                typeof(T).Name, hashKey);
            throw;
        }
    }

    public async Task<QueryResult<T>> QueryAsync<T>(QueryRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            QueryResponse? response = await _dynamoDbClient.QueryAsync(request, cancellationToken);
            var items = new List<T>();

            foreach (Dictionary<string, AttributeValue>? item in response.Items)
            {
                Document? doc = Document.FromAttributeMap(item);
                var entity = _context.FromDocument<T>(doc);
                items.Add(entity);
            }

            return new QueryResult<T>
            {
                Items = items,
                LastEvaluatedKey = response.LastEvaluatedKey,
                Count = response.Count ?? 0,
                ScannedCount = response.ScannedCount ?? 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing query on table {TableName}", request.TableName);
            throw;
        }
    }

    public async Task<IEnumerable<T>> ScanAsync<T>(ScanFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            AsyncSearch<T> scan;

            if (filter != null)
            {
                var conditions = new List<ScanCondition>();
                foreach (ScanCondition condition in filter.ToConditions())
                    conditions.Add(condition);
                var s = _context.ScanAsync<T>(conditions);
                scan = (AsyncSearch<T>)s;
            }
            else
            {
                var s = _context.ScanAsync<T>(new List<ScanCondition>());
                scan = (AsyncSearch<T>)s;
            }

            var results = new List<T>();
            List<T>? search = await scan.GetRemainingAsync(cancellationToken);
            results.AddRange(search);

            _logger.LogDebug("Scan returned {Count} entities of type {EntityType}", results.Count,
                typeof(T).Name);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning entities of type {EntityType}", typeof(T).Name);
            throw;
        }
    }

    public async Task UpdateItemAsync(string tableName,
        Dictionary<string, AttributeValue> key,
        Dictionary<string, AttributeValueUpdate> updates,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new UpdateItemRequest
            {
                TableName = tableName,
                Key = key,
                AttributeUpdates = updates
            };

            await _dynamoDbClient.UpdateItemAsync(request, cancellationToken);
            _logger.LogDebug("Updated item in table {TableName}", tableName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating item in table {TableName}", tableName);
            throw;
        }
    }

    public async Task TransactWriteAsync(TransactWriteItemsRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _dynamoDbClient.TransactWriteItemsAsync(request, cancellationToken);
            _logger.LogDebug("Executed transactional write with {Count} items",
                request.TransactItems.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing transactional write");
            throw;
        }
    }
}