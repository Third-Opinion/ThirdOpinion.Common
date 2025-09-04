using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;

namespace ThirdOpinion.Common.Aws.DynamoDb.Filters;

/// <summary>
///     Represents a query filter for DynamoDB operations
/// </summary>
public class QueryFilter
{
    private readonly List<ScanCondition> _conditions = new();

    /// <summary>
    ///     Add an equality condition
    /// </summary>
    public QueryFilter Equal(string attributeName, object value)
    {
        _conditions.Add(new ScanCondition(attributeName, ScanOperator.Equal, value));
        return this;
    }

    /// <summary>
    ///     Add a not equal condition
    /// </summary>
    public QueryFilter NotEqual(string attributeName, object value)
    {
        _conditions.Add(new ScanCondition(attributeName, ScanOperator.NotEqual, value));
        return this;
    }

    /// <summary>
    ///     Add a less than condition
    /// </summary>
    public QueryFilter LessThan(string attributeName, object value)
    {
        _conditions.Add(new ScanCondition(attributeName, ScanOperator.LessThan, value));
        return this;
    }

    /// <summary>
    ///     Add a less than or equal condition
    /// </summary>
    public QueryFilter LessThanOrEqual(string attributeName, object value)
    {
        _conditions.Add(new ScanCondition(attributeName, ScanOperator.LessThanOrEqual, value));
        return this;
    }

    /// <summary>
    ///     Add a greater than condition
    /// </summary>
    public QueryFilter GreaterThan(string attributeName, object value)
    {
        _conditions.Add(new ScanCondition(attributeName, ScanOperator.GreaterThan, value));
        return this;
    }

    /// <summary>
    ///     Add a greater than or equal condition
    /// </summary>
    public QueryFilter GreaterThanOrEqual(string attributeName, object value)
    {
        _conditions.Add(new ScanCondition(attributeName, ScanOperator.GreaterThanOrEqual, value));
        return this;
    }

    /// <summary>
    ///     Add a between condition
    /// </summary>
    public QueryFilter Between(string attributeName, object lowerBound, object upperBound)
    {
        _conditions.Add(new ScanCondition(attributeName, ScanOperator.Between, lowerBound,
            upperBound));
        return this;
    }

    /// <summary>
    ///     Add a begins with condition
    /// </summary>
    public QueryFilter BeginsWith(string attributeName, string value)
    {
        _conditions.Add(new ScanCondition(attributeName, ScanOperator.BeginsWith, value));
        return this;
    }

    /// <summary>
    ///     Add a contains condition
    /// </summary>
    public QueryFilter Contains(string attributeName, string value)
    {
        _conditions.Add(new ScanCondition(attributeName, ScanOperator.Contains, value));
        return this;
    }

    /// <summary>
    ///     Add an IN condition
    /// </summary>
    public QueryFilter In(string attributeName, params object[] values)
    {
        _conditions.Add(new ScanCondition(attributeName, ScanOperator.In, values));
        return this;
    }

    /// <summary>
    ///     Add an attribute exists condition
    /// </summary>
    public QueryFilter AttributeExists(string attributeName)
    {
        _conditions.Add(new ScanCondition(attributeName, ScanOperator.IsNotNull));
        return this;
    }

    /// <summary>
    ///     Add an attribute not exists condition
    /// </summary>
    public QueryFilter AttributeNotExists(string attributeName)
    {
        _conditions.Add(new ScanCondition(attributeName, ScanOperator.IsNull));
        return this;
    }

    /// <summary>
    ///     Convert to scan conditions
    /// </summary>
    internal IEnumerable<ScanCondition> ToConditions()
    {
        return _conditions;
    }
}

/// <summary>
///     Scan filter for DynamoDB scan operations
/// </summary>
public class ScanFilter : QueryFilter
{
    // ScanFilter inherits all functionality from QueryFilter
}