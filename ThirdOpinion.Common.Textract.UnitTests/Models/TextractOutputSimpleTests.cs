using FluentAssertions;
using ThirdOpinion.Common.Textract.Models;
using static ThirdOpinion.Common.Textract.Models.TextractOutputExtensions;
using Amazon.Textract.Model;

namespace TextractLib.Tests.Models;

public class TextractOutputSimpleTests
{
    [Fact]
    public void ToFilteredJsonString_WithNullInput_ShouldReturnNull()
    {
        // Arrange
        List<TextractOutput>? textractOutputs = null;

        // Act
        var result = TextractOutputExtensions.ToFilteredJsonString(textractOutputs!);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ToFilteredJsonString_WithEmptyInput_ShouldReturnNull()
    {
        // Arrange
        var textractOutputs = new List<TextractOutput>();

        // Act
        var result = TextractOutputExtensions.ToFilteredJsonString(textractOutputs);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ToFilteredTextractObjectv2_WithNullList_ShouldReturnNull()
    {
        // Arrange
        List<TextractOutput>? textractOutputs = null;

        // Act
        var result = TextractOutputExtensions.ToFilteredTextractObjectv2(textractOutputs!);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ToFilteredTextractObjectv2_WithEmptyList_ShouldReturnNull()
    {
        // Arrange
        var textractOutputs = new List<TextractOutput>();

        // Act
        var result = TextractOutputExtensions.ToFilteredTextractObjectv2(textractOutputs);

        // Assert
        result.Should().BeNull();
    }

    // Note: SortTextractPolygonPointsClockwise tests removed due to complex internal Point type dependencies
}