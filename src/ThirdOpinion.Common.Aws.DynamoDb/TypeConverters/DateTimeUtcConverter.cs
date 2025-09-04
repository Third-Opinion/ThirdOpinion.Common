using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using System.Globalization;

namespace ThirdOpinion.Common.Aws.DynamoDb.TypeConverters;

/// <summary>
///     Type converter for DateTime to DynamoDB ISO 8601 string
///     Always stores and retrieves as UTC
/// </summary>
public class DateTimeUtcConverter : IPropertyConverter
{
    public DynamoDBEntry ToEntry(object value)
    {
        if (value is DateTime dateTime)
        {
            // Ensure we always store as UTC
            DateTime utcDateTime = dateTime.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
                : dateTime.ToUniversalTime();

            return new Primitive { Value = utcDateTime.ToString("O") }; // ISO 8601 format
        }

        throw new ArgumentException("Value must be a DateTime", nameof(value));
    }

    public object FromEntry(DynamoDBEntry entry)
    {
        if (entry is Primitive { Value: string stringValue } &&
            DateTime.TryParse(stringValue, null, DateTimeStyles.RoundtripKind, out DateTime dateTime))
            return dateTime;
        throw new ArgumentException("Entry must be a string primitive containing a valid DateTime",
            nameof(entry));
    }
}