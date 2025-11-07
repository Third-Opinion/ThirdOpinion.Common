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
    /// <param name="attributeName">The name of the attribute to filter on</param>
    /// <param name="value">The value to compare against</param>
    /// <returns>This QueryFilter instance for method chaining</returns>
    public QueryFilter Equal(string attributeName, object value)
    {
        _conditions.Add(new ScanCondition(attributeName, ScanOperator.Equal, value));
        return this;
    }

    /// <summary>
    ///     Add a not equal condition
    /// </summary>
    /// <param name="attributeName">The name of the attribute to filter on</param>
    /// <param name="value">The value to compare against</param>
    /// <returns>This QueryFilter instance for method chaining</returns>
    public QueryFilter NotEqual(string attributeName, object value)
    {
        _conditions.Add(new ScanCondition(attributeName, ScanOperator.NotEqual, value));
        return this;
    }

    /// <summary>
    ///     Add a less than condition
    /// </summary>
    /// <param name="attributeName">The name of the attribute to filter on</param>
    /// <param name="value">The value to compare against</param>
    /// <returns>This QueryFilter instance for method chaining</returns>
    public QueryFilter LessThan(string attributeName, object value)
    {
        _conditions.Add(new ScanCondition(attributeName, ScanOperator.LessThan, value));
        return this;
    }

    /// <summary>
    ///     Add a less than or equal condition
    /// </summary>
    /// <param name="attributeName">The name of the attribute to filter on</param>
    /// <param name="value">The value to compare against</param>
    /// <returns>This QueryFilter instance for method chaining</returns>
    public QueryFilter LessThanOrEqual(string attributeName, object value)
    {
        _conditions.Add(new ScanCondition(attributeName, ScanOperator.LessThanOrEqual, value));
        return this;
    }

    /// <summary>
    ///     Add a greater than condition
    /// </summary>
    /// <param name="attributeName">The name of the attribute to filter on</param>
    /// <param name="value">The value to compare against</param>
    /// <returns>This QueryFilter instance for method chaining</returns>
    public QueryFilter GreaterThan(string attributeName, object value)
    {
        _conditions.Add(new ScanCondition(attributeName, ScanOperator.GreaterThan, value));
        return this;
    }

    /// <summary>
    ///     Add a greater than or equal condition
    /// </summary>
    /// <param name="attributeName">The name of the attribute to filter on</param>
    /// <param name="value">The value to compare against</param>
    /// <returns>This QueryFilter instance for method chaining</returns>
    public QueryFilter GreaterThanOrEqual(string attributeName, object value)
    {
        _conditions.Add(new ScanCondition(attributeName, ScanOperator.GreaterThanOrEqual, value));
        return this;
    }

    /// <summary>
    ///     Add a between condition
    /// </summary>
    /// <param name="attributeName">The name of the attribute to filter on</param>
    /// <param name="lowerBound">The lower bound value</param>
    /// <param name="upperBound">The upper bound value</param>
    /// <returns>This QueryFilter instance for method chaining</returns>
    public QueryFilter Between(string attributeName, object lowerBound, object upperBound)
    {
        _conditions.Add(new ScanCondition(attributeName, ScanOperator.Between, lowerBound,
            upperBound));
        return this;
    }

    /// <summary>
    ///     Add a begins with condition
    /// </summary>
    /// <param name="attributeName">The name of the attribute to filter on</param>
    /// <param name="value">The prefix string to match</param>
    /// <returns>This QueryFilter instance for method chaining</returns>
    public QueryFilter BeginsWith(string attributeName, string value)
    {
        _conditions.Add(new ScanCondition(attributeName, ScanOperator.BeginsWith, value));
        return this;
    }

    /// <summary>
    ///     Add a contains condition
    /// </summary>
    /// <param name="attributeName">The name of the attribute to filter on</param>
    /// <param name="value">The substring to search for</param>
    /// <returns>This QueryFilter instance for method chaining</returns>
    public QueryFilter Contains(string attributeName, string value)
    {
        _conditions.Add(new ScanCondition(attributeName, ScanOperator.Contains, value));
        return this;
    }

    /// <summary>
    ///     Add an IN condition
    /// </summary>
    /// <param name="attributeName">The name of the attribute to filter on</param>
    /// <param name="values">The array of values to match against</param>
    /// <returns>This QueryFilter instance for method chaining</returns>
    public QueryFilter In(string attributeName, params object[] values)
    {
        _conditions.Add(new ScanCondition(attributeName, ScanOperator.In, values));
        return this;
    }

    /// <summary>
    ///     Add an attribute exists condition
    /// </summary>
    /// <param name="attributeName">The name of the attribute to check for existence</param>
    /// <returns>This QueryFilter instance for method chaining</returns>
    public QueryFilter AttributeExists(string attributeName)
    {
        _conditions.Add(new ScanCondition(attributeName, ScanOperator.IsNotNull));
        return this;
    }

    /// <summary>
    ///     Add an attribute not exists condition
    /// </summary>
    /// <param name="attributeName">The name of the attribute to check for non-existence</param>
    /// <returns>This QueryFilter instance for method chaining</returns>
    public QueryFilter AttributeNotExists(string attributeName)
    {
        _conditions.Add(new ScanCondition(attributeName, ScanOperator.IsNull));
        return this;
    }

    /// <summary>
    ///     Convert to scan conditions
    /// </summary>
    /// <returns>An enumerable of ScanCondition objects representing the filter conditions</returns>
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