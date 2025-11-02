using Amazon.DynamoDBv2.DocumentModel;
using ThirdOpinion.Common.Aws.DynamoDb.TypeConverters;

namespace ThirdOpinion.Common.Aws.Tests.DynamoDb;

public class TypeConvertersTests
{
    public enum TestEnum
    {
        None = 0,
        First = 1,
        Second = 2,
        Third = 3
    }

    [Fact]
    public void EnumConverter_ToEntry_ValidEnum_ReturnsCorrectPrimitive()
    {
        // Arrange
        var converter = new EnumConverter<TestEnum>();
        var enumValue = TestEnum.Second;

        // Act
        var result = converter.ToEntry(enumValue);

        // Assert
        var primitive = result.ShouldBeOfType<Primitive>();
        primitive.Value.ShouldBe("Second");
    }

    [Fact]
    public void EnumConverter_ToEntry_NullValue_ReturnsNullPrimitive()
    {
        // Arrange
        var converter = new EnumConverter<TestEnum>();

        // Act
        var result = converter.ToEntry(null);

        // Assert
        var primitive = result.ShouldBeOfType<Primitive>();
        primitive.Value.ShouldBeNull();
    }

    [Fact]
    public void EnumConverter_ToEntry_InvalidType_ThrowsArgumentException()
    {
        // Arrange
        var converter = new EnumConverter<TestEnum>();
        var invalidValue = "not an enum";

        // Act & Assert
        ArgumentException? exception
            = Should.Throw<ArgumentException>(() => converter.ToEntry(invalidValue));
        exception.Message.ShouldBe("Value must be an enum or null");
    }

    [Fact]
    public void EnumConverter_FromEntry_ValidString_ReturnsCorrectEnum()
    {
        // Arrange
        var converter = new EnumConverter<TestEnum>();
        var primitive = new Primitive { Value = "Third" };

        // Act
        var result = converter.FromEntry(primitive);

        // Assert
        result.ShouldBe(TestEnum.Third);
    }

    [Fact]
    public void EnumConverter_FromEntry_NullPrimitive_ReturnsNull()
    {
        // Arrange
        var converter = new EnumConverter<TestEnum>();

        // Act
        var result = converter.FromEntry(null);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void EnumConverter_FromEntry_NullValue_ReturnsNull()
    {
        // Arrange
        var converter = new EnumConverter<TestEnum>();
        var primitive = new Primitive { Value = null };

        // Act
        var result = converter.FromEntry(primitive);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void EnumConverter_FromEntry_EmptyString_ReturnsNull()
    {
        // Arrange
        var converter = new EnumConverter<TestEnum>();
        var primitive = new Primitive { Value = "" };

        // Act
        var result = converter.FromEntry(primitive);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void EnumConverter_FromEntry_NonStringValue_ThrowsArgumentException()
    {
        // Arrange
        var converter = new EnumConverter<TestEnum>();
        var primitive = new Primitive { Value = 123 };

        // Act & Assert
        ArgumentException? exception
            = Should.Throw<ArgumentException>(() => converter.FromEntry(primitive));
        exception.Message.ShouldBe("Value must be a string");
    }

    [Fact]
    public void EnumConverter_FromEntry_InvalidEnumString_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var converter = new EnumConverter<TestEnum>();
        var primitive = new Primitive { Value = "InvalidEnum" };

        // Act & Assert
        ArgumentOutOfRangeException? exception
            = Should.Throw<ArgumentOutOfRangeException>(() => converter.FromEntry(primitive));
        exception.Message.ShouldContain("Value InvalidEnum is not a valid enum");
    }

    [Fact]
    public void EnumConverter_RoundTrip_PreservesValue()
    {
        // Arrange
        var converter = new EnumConverter<TestEnum>();
        var originalValue = TestEnum.First;

        // Act
        var entry = converter.ToEntry(originalValue);
        var result = converter.FromEntry(entry);

        // Assert
        result.ShouldBe(originalValue);
    }

    [Fact]
    public void NullableEnumConverter_ToEntry_ValidNullableEnum_ReturnsCorrectPrimitive()
    {
        // Arrange
        var converter = new NullableEnumConverter<TestEnum>();
        TestEnum? enumValue = TestEnum.Second;

        // Act
        var result = converter.ToEntry(enumValue);

        // Assert
        var primitive = result.ShouldBeOfType<Primitive>();
        primitive.Value.ShouldBe("Second");
    }

    [Fact]
    public void NullableEnumConverter_ToEntry_NullNullableEnum_ReturnsNullPrimitive()
    {
        // Arrange
        var converter = new NullableEnumConverter<TestEnum>();
        TestEnum? enumValue = null;

        // Act
        var result = converter.ToEntry(enumValue);

        // Assert
        var primitive = result.ShouldBeOfType<Primitive>();
        primitive.Value.ShouldBeNull();
    }

    [Fact]
    public void NullableEnumConverter_ToEntry_RegularEnum_ReturnsCorrectPrimitive()
    {
        // Arrange
        var converter = new NullableEnumConverter<TestEnum>();
        var enumValue = TestEnum.Third;

        // Act
        var result = converter.ToEntry(enumValue);

        // Assert
        var primitive = result.ShouldBeOfType<Primitive>();
        primitive.Value.ShouldBe("Third");
    }

    [Fact]
    public void NullableEnumConverter_FromEntry_ValidString_ReturnsNullableEnum()
    {
        // Arrange
        var converter = new NullableEnumConverter<TestEnum>();
        var primitive = new Primitive { Value = "First" };

        // Act
        var result = converter.FromEntry(primitive);

        // Assert
        result.ShouldNotBeNull();
        // When a nullable enum has a value, it gets boxed as the underlying enum type
        result.ShouldBeOfType<TestEnum>();
        result.ShouldBe(TestEnum.First);
    }

    [Fact]
    public void NullableEnumConverter_FromEntry_NullValue_ReturnsNull()
    {
        // Arrange
        var converter = new NullableEnumConverter<TestEnum>();
        var primitive = new Primitive { Value = null };

        // Act
        var result = converter.FromEntry(primitive);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void NullableEnumConverter_RoundTrip_PreservesNullableValue()
    {
        // Arrange
        var converter = new NullableEnumConverter<TestEnum>();
        TestEnum? originalValue = TestEnum.Second;

        // Act
        var entry = converter.ToEntry(originalValue);
        var result = converter.FromEntry(entry);

        // Assert
        // When a nullable enum has a value, it gets boxed as the underlying enum type
        var nullableResult = result.ShouldBeOfType<TestEnum>();
        nullableResult.ShouldBe(originalValue.Value);
    }

    [Fact]
    public void NullableEnumConverter_RoundTrip_PreservesNull()
    {
        // Arrange
        var converter = new NullableEnumConverter<TestEnum>();
        TestEnum? originalValue = null;

        // Act
        var entry = converter.ToEntry(originalValue);
        var result = converter.FromEntry(entry);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void DateTimeUtcConverter_ToEntry_ValidUtcDateTime_ReturnsIsoString()
    {
        // Arrange
        var converter = new DateTimeUtcConverter();
        var dateTime = new DateTime(2023, 12, 25, 10, 30, 45, DateTimeKind.Utc);

        // Act
        var result = converter.ToEntry(dateTime);

        // Assert
        var primitive = result.ShouldBeOfType<Primitive>();
        primitive.Value.ShouldBe("2023-12-25T10:30:45.0000000Z");
    }

    [Fact]
    public void DateTimeUtcConverter_ToEntry_UnspecifiedDateTime_TreatsAsUtc()
    {
        // Arrange
        var converter = new DateTimeUtcConverter();
        var dateTime = new DateTime(2023, 12, 25, 10, 30, 45, DateTimeKind.Unspecified);

        // Act
        var result = converter.ToEntry(dateTime);

        // Assert
        var primitive = result.ShouldBeOfType<Primitive>();
        primitive.Value.ShouldBe("2023-12-25T10:30:45.0000000Z");
    }

    [Fact]
    public void DateTimeUtcConverter_ToEntry_LocalDateTime_ConvertsToUtc()
    {
        // Arrange
        var converter = new DateTimeUtcConverter();
        var localDateTime = new DateTime(2023, 12, 25, 10, 30, 45, DateTimeKind.Local);

        // Act
        var result = converter.ToEntry(localDateTime);

        // Assert
        var primitive = result.ShouldBeOfType<Primitive>();
        // The exact value depends on the local timezone, but should be a valid ISO string
        primitive.Value.ShouldBeOfType<string>();
        Assert.EndsWith("Z", (string)primitive.Value);
    }

    [Fact]
    public void DateTimeUtcConverter_ToEntry_NonDateTimeValue_ThrowsArgumentException()
    {
        // Arrange
        var converter = new DateTimeUtcConverter();
        var invalidValue = "not a datetime";

        // Act & Assert
        ArgumentException? exception
            = Should.Throw<ArgumentException>(() => converter.ToEntry(invalidValue));
        exception.Message.ShouldBe("Value must be a DateTime (Parameter 'value')");
    }

    [Fact]
    public void DateTimeUtcConverter_FromEntry_ValidIsoString_ReturnsDateTime()
    {
        // Arrange
        var converter = new DateTimeUtcConverter();
        var primitive = new Primitive { Value = "2023-12-25T10:30:45.0000000Z" };

        // Act
        var result = converter.FromEntry(primitive);

        // Assert
        var dateTime = result.ShouldBeOfType<DateTime>();
        Assert.Equal(new DateTime(2023, 12, 25, 10, 30, 45, DateTimeKind.Utc), dateTime);
        dateTime.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public void DateTimeUtcConverter_FromEntry_InvalidDateString_ThrowsArgumentException()
    {
        // Arrange
        var converter = new DateTimeUtcConverter();
        var primitive = new Primitive { Value = "invalid-date" };

        // Act & Assert
        ArgumentException? exception
            = Should.Throw<ArgumentException>(() => converter.FromEntry(primitive));
        exception.Message.ShouldBe(
            "Entry must be a string primitive containing a valid DateTime (Parameter 'entry')");
    }

    [Fact]
    public void DateTimeUtcConverter_FromEntry_NonPrimitive_ThrowsArgumentException()
    {
        // Arrange
        var converter = new DateTimeUtcConverter();
        var document = new Document();

        // Act & Assert
        ArgumentException? exception
            = Should.Throw<ArgumentException>(() => converter.FromEntry(document));
        exception.Message.ShouldBe(
            "Entry must be a string primitive containing a valid DateTime (Parameter 'entry')");
    }

    [Fact]
    public void DateTimeUtcConverter_RoundTrip_PreservesValue()
    {
        // Arrange
        var converter = new DateTimeUtcConverter();
        var originalDateTime = new DateTime(2023, 6, 15, 14, 30, 0, DateTimeKind.Utc);

        // Act
        var entry = converter.ToEntry(originalDateTime);
        var result = converter.FromEntry(entry);

        // Assert
        var resultDateTime = result.ShouldBeOfType<DateTime>();
        resultDateTime.ShouldBe(originalDateTime);
        resultDateTime.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public void GuidTypeConverter_ToEntry_ValidGuid_ReturnsGuidString()
    {
        // Arrange
        var converter = new GuidTypeConverter();
        Guid guid = Guid.Parse("550e8400-e29b-41d4-a716-446655440000");

        // Act
        var result = converter.ToEntry(guid);

        // Assert
        var primitive = result.ShouldBeOfType<Primitive>();
        primitive.Value.ShouldBe("550e8400-e29b-41d4-a716-446655440000");
    }

    [Fact]
    public void GuidTypeConverter_FromEntry_ValidGuidString_ReturnsGuid()
    {
        // Arrange
        var converter = new GuidTypeConverter();
        var primitive = new Primitive { Value = "550e8400-e29b-41d4-a716-446655440000" };

        // Act
        var result = converter.FromEntry(primitive);

        // Assert
        var guid = result.ShouldBeOfType<Guid>();
        guid.ShouldBe(Guid.Parse("550e8400-e29b-41d4-a716-446655440000"));
    }

    [Fact]
    public void GuidTypeConverter_ToEntry_NonGuidValue_ThrowsArgumentException()
    {
        // Arrange
        var converter = new GuidTypeConverter();
        var invalidValue = "not a guid";

        // Act & Assert
        ArgumentException? exception
            = Should.Throw<ArgumentException>(() => converter.ToEntry(invalidValue));
        exception.Message.ShouldBe("Value must be a Guid (Parameter 'value')");
    }

    [Fact]
    public void GuidTypeConverter_FromEntry_InvalidGuidString_ThrowsArgumentException()
    {
        // Arrange
        var converter = new GuidTypeConverter();
        var primitive = new Primitive { Value = "invalid-guid" };

        // Act & Assert
        ArgumentException? exception
            = Should.Throw<ArgumentException>(() => converter.FromEntry(primitive));
        exception.Message.ShouldBe(
            "Entry must be a string primitive containing a valid Guid (Parameter 'entry')");
    }

    [Fact]
    public void GuidTypeConverter_FromEntry_NonPrimitive_ThrowsArgumentException()
    {
        // Arrange
        var converter = new GuidTypeConverter();
        var document = new Document();

        // Act & Assert
        ArgumentException? exception
            = Should.Throw<ArgumentException>(() => converter.FromEntry(document));
        exception.Message.ShouldBe(
            "Entry must be a string primitive containing a valid Guid (Parameter 'entry')");
    }

    [Fact]
    public void GuidTypeConverter_RoundTrip_PreservesValue()
    {
        // Arrange
        var converter = new GuidTypeConverter();
        var originalGuid = Guid.NewGuid();

        // Act
        var entry = converter.ToEntry(originalGuid);
        var result = converter.FromEntry(entry);

        // Assert
        result.ShouldBe(originalGuid);
    }
}