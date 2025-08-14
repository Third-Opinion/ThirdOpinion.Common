using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;

// Converts Enum to string and vice-versa.
namespace ThirdOpinion.Common.Aws.DynamoDb.TypeConverters;

// Converts Enum to string and vice-versa.
// Assume the value is never null e.g. it is always an Enum string and set 
public class EnumConverter<T> : IPropertyConverter where T : struct, Enum
{
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

    public object FromEntry(DynamoDBEntry entry)
    {
        var primitive = entry as Primitive;

        // Return null if primitive is null or its value is null/empty
        if (primitive == null || primitive.Value == null ||
            (primitive.Value is string strValue && string.IsNullOrEmpty(strValue)))
            return null;

        if (!(primitive.Value is string))
            throw new ArgumentException("Value must be a string");

        if (!Enum.TryParse(typeof(T), (string)primitive.Value, out var result))
            throw new ArgumentOutOfRangeException($"Value {primitive.Value} is not a valid enum");

        return result;
    }
}

public class NullableEnumConverter<T> : IPropertyConverter where T : struct, Enum
{
    private readonly EnumConverter<T> _enumConverter = new();

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

            var hasValue = (bool)hasValueProperty.GetValue(value);
            if (!hasValue)
                return new Primitive { Value = null };

            // Get the actual enum value
            var enumValue = valueProperty.GetValue(value);
            return _enumConverter.ToEntry(enumValue);
        }

        // Fall back to regular converter
        return _enumConverter.ToEntry(value);
    }

    public object FromEntry(DynamoDBEntry entry)
    {
        var result = _enumConverter.FromEntry(entry);
        if (result == null)
            return null;

        // Convert to nullable enum
        return Activator.CreateInstance(typeof(Nullable<>).MakeGenericType(typeof(T)), result);
    }
}