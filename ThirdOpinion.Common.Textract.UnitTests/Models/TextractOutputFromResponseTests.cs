using Amazon.Textract.Model;
using FluentAssertions;
using ThirdOpinion.Common.Textract.Models;

namespace TextractLib.Tests.Models;

public class TextractOutputFromResponseTests
{
    [Fact]
    public void FromAmazonTextractResponse_WithDetectDocumentTextResponse_ShouldCalculatePageCountFromBlocks()
    {
        // Arrange - Create a mock DetectDocumentTextResponse
        var response = new DetectDocumentTextResponse
        {
            DetectDocumentTextModelVersion = "1.0",
            DocumentMetadata = new Amazon.Textract.Model.DocumentMetadata(),
            Blocks = new List<Amazon.Textract.Model.Block>
            {
                new()
                {
                    BlockType = "PAGE",
                    Id = "page1",
                    Geometry = new Amazon.Textract.Model.Geometry
                    {
                        BoundingBox = new Amazon.Textract.Model.BoundingBox { Width = 1, Height = 1, Left = 0, Top = 0 },
                        Polygon = new List<Amazon.Textract.Model.Point> { new() { X = 0, Y = 0 }, new() { X = 1, Y = 0 }, new() { X = 1, Y = 1 }, new() { X = 0, Y = 1 } }
                    },
                    Page = 1
                },
                new()
                {
                    BlockType = "PAGE",
                    Id = "page2",
                    Geometry = new Amazon.Textract.Model.Geometry
                    {
                        BoundingBox = new Amazon.Textract.Model.BoundingBox { Width = 1, Height = 1, Left = 0, Top = 0 },
                        Polygon = new List<Amazon.Textract.Model.Point> { new() { X = 0, Y = 0 }, new() { X = 1, Y = 0 }, new() { X = 1, Y = 1 }, new() { X = 0, Y = 1 } }
                    },
                    Page = 2
                },
                new()
                {
                    BlockType = "LINE",
                    Id = "line1",
                    Text = "Sample text",
                    Confidence = 99.5f,
                    Geometry = new Amazon.Textract.Model.Geometry
                    {
                        BoundingBox = new Amazon.Textract.Model.BoundingBox { Width = 0.5f, Height = 0.1f, Left = 0.1f, Top = 0.1f },
                        Polygon = new List<Amazon.Textract.Model.Point> { new() { X = 0.1f, Y = 0.1f }, new() { X = 0.6f, Y = 0.1f }, new() { X = 0.6f, Y = 0.2f }, new() { X = 0.1f, Y = 0.2f } }
                    },
                    Page = 1
                }
            }
        };

        // Act
        var result = TextractOutputExtensions.TextractOutput.FromAmazonTextractResponse(response);

        // Assert
        result.Should().NotBeNull();
        result.Pages.Should().Be(2, "because there are 2 PAGE blocks");
        result.DocumentMetadata.Should().NotBeNull();
        result.DocumentMetadata.Pages.Should().Be(2, "because page count should be calculated from PAGE blocks");
        result.Blocks.Should().HaveCount(3, "because all blocks should be converted");
        result.DetectDocumentTextModelVersion.Should().Be("1.0");
        result.AnalyzeDocumentModelVersion.Should().Be("1.0");
    }

    [Fact]
    public void FromAmazonTextractResponse_WithNullDocumentMetadata_ShouldHandleGracefully()
    {
        // Arrange - Create a response with null DocumentMetadata
        var response = new DetectDocumentTextResponse
        {
            DetectDocumentTextModelVersion = "1.0",
            DocumentMetadata = null,
            Blocks = new List<Amazon.Textract.Model.Block>
            {
                new()
                {
                    BlockType = "PAGE",
                    Id = "page1",
                    Geometry = new Amazon.Textract.Model.Geometry
                    {
                        BoundingBox = new Amazon.Textract.Model.BoundingBox { Width = 1, Height = 1, Left = 0, Top = 0 },
                        Polygon = new List<Amazon.Textract.Model.Point> { new() { X = 0, Y = 0 }, new() { X = 1, Y = 0 }, new() { X = 1, Y = 1 }, new() { X = 0, Y = 1 } }
                    },
                    Page = 1
                }
            }
        };

        // Act
        var result = TextractOutputExtensions.TextractOutput.FromAmazonTextractResponse(response);

        // Assert
        result.Should().NotBeNull();
        result.Pages.Should().Be(1, "because there is 1 PAGE block");
        result.DocumentMetadata.Should().BeNull("because original DocumentMetadata was null");
    }

    [Fact]
    public void FromAmazonTextractResponse_WithNoPageBlocks_ShouldReturnZeroPages()
    {
        // Arrange - Create a response with no PAGE blocks
        var response = new DetectDocumentTextResponse
        {
            DetectDocumentTextModelVersion = "1.0",
            DocumentMetadata = new Amazon.Textract.Model.DocumentMetadata(),
            Blocks = new List<Amazon.Textract.Model.Block>
            {
                new()
                {
                    BlockType = "LINE",
                    Id = "line1",
                    Text = "Sample text",
                    Confidence = 99.5f,
                    Geometry = new Amazon.Textract.Model.Geometry
                    {
                        BoundingBox = new Amazon.Textract.Model.BoundingBox { Width = 0.5f, Height = 0.1f, Left = 0.1f, Top = 0.1f },
                        Polygon = new List<Amazon.Textract.Model.Point> { new() { X = 0.1f, Y = 0.1f }, new() { X = 0.6f, Y = 0.1f }, new() { X = 0.6f, Y = 0.2f }, new() { X = 0.1f, Y = 0.2f } }
                    },
                    Page = 1
                }
            }
        };

        // Act
        var result = TextractOutputExtensions.TextractOutput.FromAmazonTextractResponse(response);

        // Assert
        result.Should().NotBeNull();
        result.Pages.Should().Be(0, "because there are no PAGE blocks");
        result.DocumentMetadata.Should().NotBeNull();
        result.DocumentMetadata.Pages.Should().Be(0, "because page count should be 0 when no PAGE blocks exist");
    }

    [Fact]
    public void FromAmazonTextractResponse_WithGetDocumentTextDetectionResponse_ShouldCalculatePageCountFromBlocks()
    {
        // Arrange - Create a mock GetDocumentTextDetectionResponse
        var response = new GetDocumentTextDetectionResponse
        {
            DetectDocumentTextModelVersion = "1.0",
            DocumentMetadata = new Amazon.Textract.Model.DocumentMetadata(),
            Blocks = new List<Amazon.Textract.Model.Block>
            {
                new()
                {
                    BlockType = "PAGE",
                    Id = "page1",
                    Geometry = new Amazon.Textract.Model.Geometry
                    {
                        BoundingBox = new Amazon.Textract.Model.BoundingBox { Width = 1, Height = 1, Left = 0, Top = 0 },
                        Polygon = new List<Amazon.Textract.Model.Point> { new() { X = 0, Y = 0 }, new() { X = 1, Y = 0 }, new() { X = 1, Y = 1 }, new() { X = 0, Y = 1 } }
                    },
                    Page = 1
                }
            }
        };

        // Act
        var result = TextractOutputExtensions.TextractOutput.FromAmazonTextractResponse(response);

        // Assert
        result.Should().NotBeNull();
        result.Pages.Should().Be(1, "because there is 1 PAGE block");
        result.DocumentMetadata.Should().NotBeNull();
        result.DocumentMetadata.Pages.Should().Be(1, "because page count should be calculated from PAGE blocks");
        result.DetectDocumentTextModelVersion.Should().Be("1.0");
        result.AnalyzeDocumentModelVersion.Should().Be("1.0");
    }
}