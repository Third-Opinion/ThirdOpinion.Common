using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;

namespace ThirdOpinion.Common.Aws.DynamoDb.TypeConverters;

/// <summary>
///     Type converter for Guid to DynamoDB string
/// </summary>
public class GuidTypeConverter : IPropertyConverter
{
    public DynamoDBEntry ToEntry(object value)
    {
        if (value is Guid guid) return new Primitive { Value = guid.ToString() };
        throw new ArgumentException("Value must be a Guid", nameof(value));
    }

    public object FromEntry(DynamoDBEntry entry)
    {
        if (entry is Primitive { Value: string stringValue } &&
            Guid.TryParse(stringValue, out Guid guid)) return guid;
        throw new ArgumentException("Entry must be a string primitive containing a valid Guid",
            nameof(entry));
    }
}