using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;

namespace ThirdOpinion.Common.Aws.DynamoDb.TypeConverters;

/// <summary>
///     Type converter for Guid to DynamoDB string
/// </summary>
public class GuidTypeConverter : IPropertyConverter
{
    /// <summary>
    ///     Converts a Guid object to a DynamoDB entry as a string
    /// </summary>
    /// <param name="value">The Guid value to convert</param>
    /// <returns>A DynamoDB primitive entry containing the string representation of the Guid</returns>
    /// <exception cref="ArgumentException">Thrown when the value is not a Guid</exception>
    public DynamoDBEntry ToEntry(object value)
    {
        if (value is Guid guid) return new Primitive { Value = guid.ToString() };
        throw new ArgumentException("Value must be a Guid", nameof(value));
    }

    /// <summary>
    ///     Converts a DynamoDB entry back to a Guid object
    /// </summary>
    /// <param name="entry">The DynamoDB entry to convert</param>
    /// <returns>A Guid object parsed from the entry value</returns>
    /// <exception cref="ArgumentException">Thrown when the entry is not a valid Guid string</exception>
    public object FromEntry(DynamoDBEntry entry)
    {
        if (entry is Primitive { Value: string stringValue } &&
            Guid.TryParse(stringValue, out Guid guid)) return guid;
        throw new ArgumentException("Entry must be a string primitive containing a valid Guid",
            nameof(entry));
    }
}