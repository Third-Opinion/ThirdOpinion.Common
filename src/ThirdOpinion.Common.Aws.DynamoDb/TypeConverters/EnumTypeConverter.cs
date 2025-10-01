using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;

namespace ThirdOpinion.Common.Aws.DynamoDb.TypeConverters;

/// <summary>
///     Converts enum values to string representation for DynamoDB storage and vice versa
/// </summary>
/// <typeparam name="T">The enum type to convert</typeparam>
public class EnumConverter<T> : IPropertyConverter where T : struct, Enum
{
    /// <summary>
    ///     Converts an enum value to a DynamoDB entry
    /// </summary>
    /// <param name="value">The enum value to convert</param>
    /// <returns>A DynamoDB primitive entry containing the string representation of the enum</returns>
    /// <exception cref="ArgumentException">Thrown when the value is not a valid enum type</exception>
    public DynamoDBEntry ToEntry(object value)
    {
        // If value is null, return null primitive
        if (value == null)
            return new Primitive { Value = null };

        if (value is T enumValue)
            return new Primitive
            {
                Value = enumValue.ToString()
            };

        throw new ArgumentException("Value must be an enum or null");
    }

    /// <summary>
    ///     Converts a DynamoDB entry back to an enum value
    /// </summary>
    /// <param name="entry">The DynamoDB entry to convert</param>
    /// <returns>The enum value parsed from the entry</returns>
    /// <exception cref="ArgumentException">Thrown when the entry is not a valid string</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the string value is not a valid enum value</exception>
    public object FromEntry(DynamoDBEntry entry)
    {
        var primitive = entry as Primitive;

        // Return null if primitive is null or its value is null/empty
        if (primitive == null || primitive.Value == null ||
            (primitive.Value is string strValue && string.IsNullOrEmpty(strValue)))
            return null!;

        if (!(primitive.Value is string))
            throw new ArgumentException("Value must be a string");

        if (!Enum.TryParse(typeof(T), (string)primitive.Value, out var result))
            throw new ArgumentOutOfRangeException($"Value {primitive.Value} is not a valid enum");

        return result;
    }
}

/// <summary>
///     Converts nullable enum values to string representation for DynamoDB storage and vice versa
/// </summary>
/// <typeparam name="T">The enum type to convert</typeparam>
public class NullableEnumConverter<T> : IPropertyConverter where T : struct, Enum
{
    private readonly EnumConverter<T> _enumConverter = new();

    /// <summary>
    ///     Converts a nullable enum value to a DynamoDB entry
    /// </summary>
    /// <param name="value">The nullable enum value to convert</param>
    /// <returns>A DynamoDB primitive entry containing the string representation of the enum or null</returns>
    public DynamoDBEntry ToEntry(object value)
    {
        // Check if it's a nullable enum and handle appropriately
        if (value == null)
            return new Primitive { Value = null };

        // Try to get the underlying value if it's a nullable enum
        if (value.GetType() == typeof(T?))
        {
            var nullableType = typeof(Nullable<>).MakeGenericType(typeof(T));
            var hasValueProperty = nullableType.GetProperty("HasValue");
            var valueProperty = nullableType.GetProperty("Value");

            var hasValue = (bool)(hasValueProperty?.GetValue(value) ?? false);
            if (!hasValue)
                return new Primitive { Value = null };

            // Get the actual enum value
            var enumValue = valueProperty?.GetValue(value);
            return _enumConverter.ToEntry(enumValue!);
        }

        // Fall back to regular converter
        return _enumConverter.ToEntry(value);
    }

    /// <summary>
    ///     Converts a DynamoDB entry back to a nullable enum value
    /// </summary>
    /// <param name="entry">The DynamoDB entry to convert</param>
    /// <returns>The nullable enum value parsed from the entry</returns>
    public object FromEntry(DynamoDBEntry entry)
    {
        var result = _enumConverter.FromEntry(entry);
        if (result == null)
            return null!;

        // Convert to nullable enum by boxing the value as a nullable type
        return (T?)(T)result;
    }
}