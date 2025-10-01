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

    /// <summary>
    ///     Initializes a new instance of the <see cref="DynamoDbRepository"/> class
    /// </summary>
    /// <param name="context">The DynamoDB context for data operations</param>
    /// <param name="dynamoDbClient">The low-level DynamoDB client</param>
    /// <param name="logger">The logger instance for this repository</param>
    public DynamoDbRepository(IDynamoDBContext context, IAmazonDynamoDB dynamoDbClient, ILogger<DynamoDbRepository> logger)
    {
        _context = context;
        _dynamoDbClient = dynamoDbClient;
        _logger = logger;
    }

    /// <summary>
    ///     Saves an entity to DynamoDB
    /// </summary>
    /// <typeparam name="T">The type of entity to save</typeparam>
    /// <param name="entity">The entity to save</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
    /// <returns>A task representing the asynchronous save operation</returns>
    /// <exception cref="Exception">Thrown when an error occurs during the save operation</exception>
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

    /// <summary>
    ///     Saves an entity to DynamoDB with specific operation configuration
    /// </summary>
    /// <typeparam name="T">The type of entity to save</typeparam>
    /// <param name="entity">The entity to save</param>
    /// <param name="config">Configuration options for the save operation</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
    /// <returns>A task representing the asynchronous save operation</returns>
    /// <exception cref="Exception">Thrown when an error occurs during the save operation</exception>
    public async Task SaveAsync<T>(T entity, DynamoDBOperationConfig config, CancellationToken cancellationToken = default)
    {
        try
        {
            var saveConfig = new SaveConfig
            {
                OverrideTableName = config.OverrideTableName
            };
            await _context.SaveAsync(entity, saveConfig, cancellationToken);
            _logger.LogDebug("Saved entity of type {EntityType} to DynamoDB table {TableName}", typeof(T).Name, config.OverrideTableName ?? "default");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving entity of type {EntityType} to DynamoDB table {TableName}",
                typeof(T).Name, config.OverrideTableName ?? "default");
            throw;
        }
    }

    /// <summary>
    ///     Saves multiple entities to DynamoDB in a batch operation
    /// </summary>
    /// <typeparam name="T">The type of entities to save</typeparam>
    /// <param name="entities">The collection of entities to save</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
    /// <returns>A task representing the asynchronous batch save operation</returns>
    /// <exception cref="Exception">Thrown when an error occurs during the batch save operation</exception>
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

    /// <summary>
    ///     Loads an entity from DynamoDB by its hash key and optional range key
    /// </summary>
    /// <typeparam name="T">The type of entity to load</typeparam>
    /// <param name="hashKey">The hash key of the entity</param>
    /// <param name="rangeKey">The optional range key of the entity</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
    /// <returns>A task representing the asynchronous load operation, returning the entity or null if not found</returns>
    /// <exception cref="Exception">Thrown when an error occurs during the load operation</exception>
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

    /// <summary>
    ///     Loads an entity from DynamoDB by its hash key and optional range key with specific operation configuration
    /// </summary>
    /// <typeparam name="T">The type of entity to load</typeparam>
    /// <param name="hashKey">The hash key of the entity</param>
    /// <param name="rangeKey">The optional range key of the entity</param>
    /// <param name="config">Configuration options for the load operation</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
    /// <returns>A task representing the asynchronous load operation, returning the entity or null if not found</returns>
    /// <exception cref="Exception">Thrown when an error occurs during the load operation</exception>
    public async Task<T?> LoadAsync<T>(object hashKey,
        object? rangeKey,
        DynamoDBOperationConfig config,
        CancellationToken cancellationToken = default)
        where T : class
    {
        try
        {
            var loadConfig = new LoadConfig
            {
                OverrideTableName = config.OverrideTableName,
                ConsistentRead = config.ConsistentRead
            };
            if (rangeKey == null) return await _context.LoadAsync<T>(hashKey, loadConfig, cancellationToken);
            return await _context.LoadAsync<T>(hashKey, rangeKey, loadConfig, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error loading entity of type {EntityType} with key {HashKey}/{RangeKey} from table {TableName}",
                typeof(T).Name, hashKey, rangeKey, config.OverrideTableName ?? "default");
            throw;
        }
    }

    /// <summary>
    ///     Deletes an entity from DynamoDB by its hash key and optional range key
    /// </summary>
    /// <typeparam name="T">The type of entity to delete</typeparam>
    /// <param name="hashKey">The hash key of the entity to delete</param>
    /// <param name="rangeKey">The optional range key of the entity to delete</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
    /// <returns>A task representing the asynchronous delete operation</returns>
    /// <exception cref="Exception">Thrown when an error occurs during the delete operation</exception>
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

    /// <summary>
    ///     Deletes an entity from DynamoDB by its hash key and optional range key with specific operation configuration
    /// </summary>
    /// <typeparam name="T">The type of entity to delete</typeparam>
    /// <param name="hashKey">The hash key of the entity to delete</param>
    /// <param name="rangeKey">The optional range key of the entity to delete</param>
    /// <param name="config">Configuration options for the delete operation</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
    /// <returns>A task representing the asynchronous delete operation</returns>
    /// <exception cref="Exception">Thrown when an error occurs during the delete operation</exception>
    public async Task DeleteAsync<T>(object hashKey,
        object? rangeKey,
        DynamoDBOperationConfig config,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var deleteConfig = new DeleteConfig
            {
                OverrideTableName = config.OverrideTableName
            };
            if (rangeKey == null)
                await _context.DeleteAsync<T>(hashKey, deleteConfig, cancellationToken);
            else
                await _context.DeleteAsync<T>(hashKey, rangeKey, deleteConfig, cancellationToken);
            _logger.LogDebug("Deleted entity of type {EntityType} with key {HashKey}/{RangeKey} from table {TableName}",
                typeof(T).Name, hashKey, rangeKey, config.OverrideTableName ?? "default");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error deleting entity of type {EntityType} with key {HashKey}/{RangeKey} from table {TableName}",
                typeof(T).Name, hashKey, rangeKey, config.OverrideTableName ?? "default");
            throw;
        }
    }

    /// <summary>
    ///     Queries DynamoDB for entities with the specified hash key and optional filter conditions
    /// </summary>
    /// <typeparam name="T">The type of entities to query</typeparam>
    /// <param name="hashKey">The hash key to query for</param>
    /// <param name="filter">Optional filter conditions to apply to the query</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
    /// <returns>A task representing the asynchronous query operation, returning a collection of entities</returns>
    /// <exception cref="Exception">Thrown when an error occurs during the query operation</exception>
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

    /// <summary>
    ///     Executes a query using a low-level QueryRequest with detailed result information
    /// </summary>
    /// <typeparam name="T">The type of entities to query</typeparam>
    /// <param name="request">The QueryRequest containing query parameters</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
    /// <returns>A task representing the asynchronous query operation, returning detailed query results</returns>
    /// <exception cref="Exception">Thrown when an error occurs during the query operation</exception>
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

    /// <summary>
    ///     Scans a DynamoDB table for entities with optional filter conditions
    /// </summary>
    /// <typeparam name="T">The type of entities to scan</typeparam>
    /// <param name="filter">Optional filter conditions to apply to the scan</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
    /// <returns>A task representing the asynchronous scan operation, returning a collection of entities</returns>
    /// <exception cref="Exception">Thrown when an error occurs during the scan operation</exception>
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

    /// <summary>
    ///     Updates specific attributes of an item in DynamoDB
    /// </summary>
    /// <param name="tableName">The name of the table containing the item to update</param>
    /// <param name="key">The primary key of the item to update</param>
    /// <param name="updates">The attribute updates to apply to the item</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
    /// <returns>A task representing the asynchronous update operation</returns>
    /// <exception cref="Exception">Thrown when an error occurs during the update operation</exception>
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

    /// <summary>
    ///     Executes a transactional write operation containing multiple write actions
    /// </summary>
    /// <param name="request">The TransactWriteItemsRequest containing the transaction items</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
    /// <returns>A task representing the asynchronous transactional write operation</returns>
    /// <exception cref="Exception">Thrown when an error occurs during the transactional write operation</exception>
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