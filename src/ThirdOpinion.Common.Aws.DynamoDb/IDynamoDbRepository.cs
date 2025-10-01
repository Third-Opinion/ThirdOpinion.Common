using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using ThirdOpinion.Common.Aws.DynamoDb.Filters;

namespace ThirdOpinion.Common.Aws.DynamoDb;

/// <summary>
///     Generic repository interface for DynamoDB operations
/// </summary>
public interface IDynamoDbRepository
{
    /// <summary>
    ///     Save an entity to DynamoDB
    /// </summary>
    Task SaveAsync<T>(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Save an entity to DynamoDB with save config
    /// </summary>
    Task SaveAsync<T>(T entity, DynamoDBOperationConfig config, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Save multiple entities in a batch
    /// </summary>
    Task BatchSaveAsync<T>(IEnumerable<T> entities, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Load an entity by hash and range key
    /// </summary>
    Task<T?> LoadAsync<T>(object hashKey,
        object? rangeKey = null,
        CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    ///     Load an entity by hash and range key with operation config
    /// </summary>
    Task<T?> LoadAsync<T>(object hashKey,
        object? rangeKey,
        DynamoDBOperationConfig config,
        CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    ///     Delete an entity
    /// </summary>
    Task DeleteAsync<T>(object hashKey,
        object? rangeKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Delete an entity with operation config
    /// </summary>
    Task DeleteAsync<T>(object hashKey,
        object? rangeKey,
        DynamoDBOperationConfig config,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Query entities by hash key with optional filters
    /// </summary>
    Task<IEnumerable<T>> QueryAsync<T>(object hashKey,
        QueryFilter? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Query entities with advanced options
    /// </summary>
    Task<QueryResult<T>> QueryAsync<T>(QueryRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Scan table with optional filters
    /// </summary>
    Task<IEnumerable<T>> ScanAsync<T>(ScanFilter? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Update an item with specific attributes
    /// </summary>
    Task UpdateItemAsync(string tableName,
        Dictionary<string, AttributeValue> key,
        Dictionary<string, AttributeValueUpdate> updates,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Execute a transactional write
    /// </summary>
    Task TransactWriteAsync(TransactWriteItemsRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Result of a query operation
/// </summary>
public class QueryResult<T>
{
    /// <summary>
    ///     Gets or sets the list of items returned by the query
    /// </summary>
    public List<T> Items { get; set; } = new();

    /// <summary>
    ///     Gets or sets the primary key of the item where the operation stopped for pagination
    /// </summary>
    public Dictionary<string, AttributeValue>? LastEvaluatedKey { get; set; }

    /// <summary>
    ///     Gets or sets the number of items in the response
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    ///     Gets or sets the number of items evaluated during the query before applying any filter expression
    /// </summary>
    public int ScannedCount { get; set; }
}