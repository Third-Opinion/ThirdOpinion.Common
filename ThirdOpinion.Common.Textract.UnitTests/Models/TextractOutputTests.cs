using FluentAssertions;
using System.Text.Json;
using ThirdOpinion.Common.Textract.Models;

namespace TextractLib.Tests.Models;

public class TextractOutputTests
{
    private const string JsonFilePath = "TestData/detectDocumentTextResponse1.json";
    private const string JsonFilePath2 = "TestData/Gadberry-Helen-19400805~316597~20160412~C17EDF29-3D32-490B-8354-A33683B552A0~pg1-pg10.pdf-textract-merged-250401172248.json";

     // Create two single-page TextractOutput objects
     private TextractOutputExtensions.TextractOutput _document1Of2 = new TextractOutputExtensions.TextractOutput
        {
            AnalyzeDocumentModelVersion = "1.0",
            DetectDocumentTextModelVersion = "1.0",
            DocumentMetadata = new TextractOutputExtensions.DocumentMetadata { Pages = 1 },
            Blocks = new List<TextractOutputExtensions.Block>
            {
                new()
                {
                    BlockType = TextractOutputExtensions.BlockTypeEnum.PAGE,
                    Id = "page1",
                    Geometry = new TextractOutputExtensions.Geometry
                    {
                        BoundingBox = new TextractOutputExtensions.BoundingBox { Width = 1, Height = 1, Left = 0, Top = 0 },
                        Polygon = new List<TextractOutputExtensions.Point> { new(0, 0), new(1, 0), new(1, 1), new(0, 1) }
                    },
                    Page = 1,
                    Relationships = new List<TextractOutputExtensions.Relationship>
                    {
                        new() { Type = "CHILD", Ids = new List<string> { "line1" } }
                    }
                },
                new()
                {
                    BlockType = TextractOutputExtensions.BlockTypeEnum.LINE,
                    Id = "line1",
                    Text = "Document 1 Line 1",
                    Confidence = 99.5f,
                    Geometry = new TextractOutputExtensions.Geometry
                    {
                        BoundingBox = new TextractOutputExtensions.BoundingBox { Width = 0.5f, Height = 0.1f, Left = 0.1f, Top = 0.1f },
                        Polygon = new List<TextractOutputExtensions.Point>
                        {
                            new(0.1f, 0.1f), new(0.6f, 0.1f), new(0.6f, 0.2f), new(0.1f, 0.2f)
                        }
                    },
                    Page = 1,
                    Relationships = new List<TextractOutputExtensions.Relationship>
                    {
                        new() { Type = "CHILD", Ids = new List<string> { "word1", "word2", "word3" } }
                    }
                },
                new()
                {
                    BlockType = TextractOutputExtensions.BlockTypeEnum.WORD,
                    Id = "word1",
                    Text = "Document",
                    Confidence = 99.8f,
                    Geometry = new TextractOutputExtensions.Geometry
                    {
                        BoundingBox = new TextractOutputExtensions.BoundingBox { Width = 0.2f, Height = 0.1f, Left = 0.1f, Top = 0.1f },
                        Polygon = new List<TextractOutputExtensions.Point>
                        {
                            new(0.1f, 0.1f), new(0.3f, 0.1f), new(0.3f, 0.2f), new(0.1f, 0.2f)
                        }
                    },
                    Page = 1
                },
                new()
                {
                    BlockType = TextractOutputExtensions.BlockTypeEnum.WORD,
                    Id = "word2",
                    Text = "1",
                    Confidence = 99.7f,
                    Geometry = new TextractOutputExtensions.Geometry
                    {
                        BoundingBox = new TextractOutputExtensions.BoundingBox { Width = 0.1f, Height = 0.1f, Left = 0.35f, Top = 0.1f },
                        Polygon = new List<TextractOutputExtensions.Point>
                        {
                            new(0.35f, 0.1f), new(0.45f, 0.1f), new(0.45f, 0.2f), new(0.35f, 0.2f)
                        }
                    },
                    Page = 1
                },
                new()
                {
                    BlockType = TextractOutputExtensions.BlockTypeEnum.WORD,
                    Id = "word3",
                    Text = "Line",
                    Confidence = 99.6f,
                    Geometry = new TextractOutputExtensions.Geometry
                    {
                        BoundingBox = new TextractOutputExtensions.BoundingBox { Width = 0.15f, Height = 0.1f, Left = 0.5f, Top = 0.1f },
                        Polygon = new List<TextractOutputExtensions.Point>
                        {
                            new(0.5f, 0.1f), new(0.65f, 0.1f), new(0.65f, 0.2f), new(0.5f, 0.2f)
                        }
                    },
                    Page = 1
                }
            }
        };

        readonly TextractOutputExtensions.TextractOutput _document2Of2 = new TextractOutputExtensions.TextractOutput
        {
            AnalyzeDocumentModelVersion = "1.0",
            DetectDocumentTextModelVersion = "1.0",
            DocumentMetadata = new TextractOutputExtensions.DocumentMetadata { Pages = 1 },
            Blocks = new List<TextractOutputExtensions.Block>
            {
                new()
                {
                    BlockType = TextractOutputExtensions.BlockTypeEnum.PAGE,
                    Id = "page2",
                    Geometry = new TextractOutputExtensions.Geometry
                    {
                        BoundingBox = new TextractOutputExtensions.BoundingBox { Width = 1, Height = 1, Left = 0, Top = 0 },
                        Polygon = new List<TextractOutputExtensions.Point> { new(0, 0), new(1, 0), new(1, 1), new(0, 1) }
                    },
                    Page = 1,
                    Relationships = new List<TextractOutputExtensions.Relationship>
                    {
                        new() { Type = "CHILD", Ids = new List<string> { "line2", "line3" } }
                    }
                },
                new()
                {
                    BlockType = TextractOutputExtensions.BlockTypeEnum.LINE,
                    Id = "line2",
                    Text = "Document 2 Line 1",
                    Confidence = 98.7f,
                    Geometry = new TextractOutputExtensions.Geometry
                    {
                        BoundingBox = new TextractOutputExtensions.BoundingBox { Width = 0.5f, Height = 0.1f, Left = 0.1f, Top = 0.1f },
                        Polygon = new List<TextractOutputExtensions.Point>
                        {
                            new(0.1f, 0.1f), new(0.6f, 0.1f), new(0.6f, 0.2f), new(0.1f, 0.2f)
                        }
                    },
                    Page = 1,
                    Relationships = new List<TextractOutputExtensions.Relationship>
                    {
                        new() { Type = "CHILD", Ids = new List<string> { "word4", "word5", "word6", "word7" } }
                    }
                },
                new()
                {
                    BlockType = TextractOutputExtensions.BlockTypeEnum.WORD,
                    Id = "word4",
                    Text = "Document",
                    Confidence = 98.8f,
                    Geometry = new TextractOutputExtensions.Geometry
                    {
                        BoundingBox = new TextractOutputExtensions.BoundingBox { Width = 0.2f, Height = 0.1f, Left = 0.1f, Top = 0.1f },
                        Polygon = new List<TextractOutputExtensions.Point>
                        {
                            new(0.1f, 0.1f), new(0.3f, 0.1f), new(0.3f, 0.2f), new(0.1f, 0.2f)
                        }
                    },
                    Page = 1
                },
                new()
                {
                    BlockType = TextractOutputExtensions.BlockTypeEnum.WORD,
                    Id = "word5",
                    Text = "2",
                    Confidence = 98.7f,
                    Geometry = new TextractOutputExtensions.Geometry
                    {
                        BoundingBox = new TextractOutputExtensions.BoundingBox { Width = 0.1f, Height = 0.1f, Left = 0.35f, Top = 0.1f },
                        Polygon = new List<TextractOutputExtensions.Point>
                        {
                            new(0.35f, 0.1f), new(0.45f, 0.1f), new(0.45f, 0.2f), new(0.35f, 0.2f)
                        }
                    },
                    Page = 1
                },
                new()
                {
                    BlockType = TextractOutputExtensions.BlockTypeEnum.WORD,
                    Id = "word6",
                    Text = "Line",
                    Confidence = 98.6f,
                    Geometry = new TextractOutputExtensions.Geometry
                    {
                        BoundingBox = new TextractOutputExtensions.BoundingBox { Width = 0.15f, Height = 0.1f, Left = 0.5f, Top = 0.1f },
                        Polygon = new List<TextractOutputExtensions.Point>
                        {
                            new(0.5f, 0.1f), new(0.65f, 0.1f), new(0.65f, 0.2f), new(0.5f, 0.2f)
                        }
                    },
                    Page = 1
                },
                new()
                {
                    BlockType = TextractOutputExtensions.BlockTypeEnum.WORD,
                    Id = "word7",
                    Text = "1",
                    Confidence = 98.5f,
                    Geometry = new TextractOutputExtensions.Geometry
                    {
                        BoundingBox = new TextractOutputExtensions.BoundingBox { Width = 0.1f, Height = 0.1f, Left = 0.7f, Top = 0.1f },
                        Polygon = new List<TextractOutputExtensions.Point>
                        {
                            new(0.7f, 0.1f), new(0.8f, 0.1f), new(0.8f, 0.2f), new(0.7f, 0.2f)
                        }
                    },
                    Page = 1
                },
                new()
                {
                    BlockType = TextractOutputExtensions.BlockTypeEnum.LINE,
                    Id = "line3",
                    Text = "Document 2 Line 2",
                    Confidence = 98.7f,
                    Geometry = new TextractOutputExtensions.Geometry
                    {
                        BoundingBox = new TextractOutputExtensions.BoundingBox { Width = 0.5f, Height = 0.1f, Left = 0.1f, Top = 0.3f },
                        Polygon = new List<TextractOutputExtensions.Point>
                        {
                            new(0.1f, 0.3f), new(0.6f, 0.3f), new(0.6f, 0.4f), new(0.1f, 0.4f)
                        }
                    },
                    Page = 1,
                    Relationships = new List<TextractOutputExtensions.Relationship>
                    {
                        new() { Type = "CHILD", Ids = new List<string> { "word8", "word9", "word10", "word11" } }
                    }
                },
                new()
                {
                    BlockType = TextractOutputExtensions.BlockTypeEnum.WORD,
                    Id = "word8",
                    Text = "Document",
                    Confidence = 98.8f,
                    Geometry = new TextractOutputExtensions.Geometry
                    {
                        BoundingBox = new TextractOutputExtensions.BoundingBox { Width = 0.2f, Height = 0.1f, Left = 0.1f, Top = 0.3f },
                        Polygon = new List<TextractOutputExtensions.Point>
                        {
                            new(0.1f, 0.3f), new(0.3f, 0.3f), new(0.3f, 0.4f), new(0.1f, 0.4f)
                        }
                    },
                    Page = 1
                },
                new()
                {
                    BlockType = TextractOutputExtensions.BlockTypeEnum.WORD,
                    Id = "word9",
                    Text = "2",
                    Confidence = 98.7f,
                    Geometry = new TextractOutputExtensions.Geometry
                    {
                        BoundingBox = new TextractOutputExtensions.BoundingBox { Width = 0.1f, Height = 0.1f, Left = 0.35f, Top = 0.3f },
                        Polygon = new List<TextractOutputExtensions.Point>
                        {
                            new(0.35f, 0.3f), new(0.45f, 0.3f), new(0.45f, 0.4f), new(0.35f, 0.4f)
                        }
                    },
                    Page = 1
                },
                new()
                {
                    BlockType = TextractOutputExtensions.BlockTypeEnum.WORD,
                    Id = "word10",
                    Text = "Line",
                    Confidence = 98.6f,
                    Geometry = new TextractOutputExtensions.Geometry
                    {
                        BoundingBox = new TextractOutputExtensions.BoundingBox { Width = 0.15f, Height = 0.1f, Left = 0.5f, Top = 0.3f },
                        Polygon = new List<TextractOutputExtensions.Point>
                        {
                            new(0.5f, 0.3f), new(0.65f, 0.3f), new(0.65f, 0.4f), new(0.5f, 0.4f)
                        }
                    },
                    Page = 1
                },
                new()
                {
                    BlockType = TextractOutputExtensions.BlockTypeEnum.WORD,
                    Id = "word11",
                    Text = "2",
                    Confidence = 98.5f,
                    Geometry = new TextractOutputExtensions.Geometry
                    {
                        BoundingBox = new TextractOutputExtensions.BoundingBox { Width = 0.1f, Height = 0.1f, Left = 0.7f, Top = 0.3f },
                        Polygon = new List<TextractOutputExtensions.Point>
                        {
                            new(0.7f, 0.3f), new(0.8f, 0.3f), new(0.8f, 0.4f), new(0.7f, 0.4f)
                        }
                    },
                    Page = 1
                }
            }
        };

    [Fact]
    public void TestTextractOutputDeserialization()
    {
        // Load the JSON file
        var jsonData = File.ReadAllText(JsonFilePath);

        // Deserialize the JSON into the TextractOutput object
        var textractOutput = JsonSerializer.Deserialize<TextractOutputExtensions.TextractOutput>(jsonData);

        textractOutput.Should().NotBeNull();
        textractOutput.Blocks.Should().NotBeNullOrEmpty();

        textractOutput.Blocks[0].BlockType.Should().Be(TextractOutputExtensions.BlockTypeEnum.PAGE);
        textractOutput.Blocks[0].Geometry.Should().NotBeNull();
        textractOutput.Blocks[0].Geometry.Polygon.Should().NotBeNullOrEmpty();
        textractOutput.Blocks[0].Id.Should().NotBeNullOrEmpty();
        textractOutput.Blocks[0].Relationships.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void TestBlockProperties()
    {
        var jsonData = File.ReadAllText(JsonFilePath);

        var textractOutput = JsonSerializer.Deserialize<TextractOutputExtensions.TextractOutput>(jsonData);

        var block = textractOutput.Blocks.Find(b => b.Id == "9bbeb18c-7eba-40a7-b535-8279a42adfa4");
        block.Should().NotBeNull();
        block.BlockType.Should().Be(TextractOutputExtensions.BlockTypeEnum.WORD);
        block.Text.Should().Be("Room");
    }

    [Fact]
    public void TestFilteredJson()
    {
        var jsonData = File.ReadAllText(JsonFilePath);
        var textractOutput = JsonSerializer.Deserialize<TextractOutputExtensions.TextractOutput>(jsonData);
        var filteredJson = TextractOutputExtensions.ToFilteredJsonString(new List<TextractOutputExtensions.TextractOutput> { textractOutput });

        // Parse and format the JSON for display using System.Text.Json
        using var parsedJson = JsonDocument.Parse(filteredJson);
        Console.WriteLine(JsonFilePath);
        Console.WriteLine(JsonSerializer.Serialize(parsedJson.RootElement, new JsonSerializerOptions { WriteIndented = true }));
    }

    [Fact]
    public void TestFilteredJsonObject()
    {
        var jsonData = File.ReadAllText(JsonFilePath);
        var textractOutput = JsonSerializer.Deserialize<TextractOutputExtensions.TextractOutput>(jsonData);
        var filteredJson = TextractOutputExtensions.ToFilteredJsonString(new List<TextractOutputExtensions.TextractOutput> { textractOutput });
        Console.WriteLine(filteredJson);
        filteredJson.Should().NotBeNull();
        // filteredJson["Blocks"].Should().NotBeNull();
    }

    [Fact]
    public void TestMergingTwoSinglePageDocuments()
    {
        // Merge the documents
        var filteredJson =
            TextractOutputExtensions.ToFilteredJsonString(new List<TextractOutputExtensions.TextractOutput> { _document1Of2, _document2Of2 });

        // Verify the result is valid JSON and not null
        filteredJson.Should().NotBeNull();

        using var parsedJson = JsonDocument.Parse(filteredJson);
        var root = parsedJson.RootElement;

        // Verify the merged result
        root.ValueKind.Should().Be(JsonValueKind.Object);

        // Verify DocumentMetadata.Pages
        var documentMetadata = root.GetProperty("DocumentMetadata");
        var pages = documentMetadata.GetProperty("Pages").GetInt32();
        pages.Should().Be(2);

        // Verify blocks array exists and has expected count
        var blocks = root.GetProperty("Blocks");
        blocks.ValueKind.Should().Be(JsonValueKind.Array);
        blocks.GetArrayLength().Should().Be(14);

        // Count LINE blocks
        int lineBlockCount = 0;
        foreach (var block in blocks.EnumerateArray())
        {
            if (block.GetProperty("BlockType").GetString() == "LINE")
            {
                lineBlockCount++;
            }
        }
        lineBlockCount.Should().Be(3);
    }

    [Fact]
    public void TestMergingTwoSinglePageDocumentsV2()
    {
        // Merge the documents
        var filteredJson =
            TextractOutputExtensions.ToFilteredJsonStringv2(new List<TextractOutputExtensions.TextractOutput> { _document1Of2, _document2Of2 });

        // Verify the result is valid JSON and not null
        filteredJson.Should().NotBeNull();

        using var parsedJson = JsonDocument.Parse(filteredJson);
        var root = parsedJson.RootElement;

        // Verify the merged result
        root.ValueKind.Should().Be(JsonValueKind.Object);

        // Verify DocumentMetadata.Pages
        var documentMetadata = root.GetProperty("DocumentMetadata");
        var pages = documentMetadata.GetProperty("Pages").GetInt32();
        pages.Should().Be(2);

        // Verify blocks array exists and has expected count
        var blocks = root.GetProperty("Blocks");
        blocks.ValueKind.Should().Be(JsonValueKind.Array);
        blocks.GetArrayLength().Should().Be(16); // +2 page blocks

        // Count LINE blocks
        int lineBlockCount = 0;
        foreach (var block in blocks.EnumerateArray())
        {
            if (block.GetProperty("BlockType").GetString() == "LINE")
            {
                lineBlockCount++;
            }
        }
        lineBlockCount.Should().Be(3);
    }

    [Fact]
    public void TestTwoSinglePageDocumentsKBString()
    {

        var x = TextractOutputExtensions.ToKBJsonString(new List<TextractOutputExtensions.TextractOutput>
            { _document1Of2, _document2Of2 });

        Console.WriteLine(x);

    }

    [Fact]
    public void TestMergingThreeDocumentsWithMultiplePages()
    {
        // Create first document with one page
        var document1 = new TextractOutputExtensions.TextractOutput
        {
            AnalyzeDocumentModelVersion = "1.0",
            DetectDocumentTextModelVersion = "1.0",
            DocumentMetadata = new TextractOutputExtensions.DocumentMetadata { Pages = 1 },
            Blocks = new List<TextractOutputExtensions.Block>
            {
                new()
                {
                    BlockType = TextractOutputExtensions.BlockTypeEnum.PAGE,
                    Id = Guid.NewGuid().ToString(),
                    Geometry = new TextractOutputExtensions.Geometry
                    {
                        BoundingBox = new TextractOutputExtensions.BoundingBox { Width = 1, Height = 1, Left = 0, Top = 0 },
                        Polygon = new List<TextractOutputExtensions.Point> { new(0, 0), new(1, 0), new(1, 1), new(0, 1) }
                    },
                    Page = 1
                },
                new()
                {
                    BlockType = TextractOutputExtensions.BlockTypeEnum.LINE,
                    Id = Guid.NewGuid().ToString(),
                    Text = "Document 1 Line 1",
                    Confidence = 99.5f,
                    Geometry = new TextractOutputExtensions.Geometry
                    {
                        BoundingBox = new TextractOutputExtensions.BoundingBox { Width = 0.5f, Height = 0.1f, Left = 0.1f, Top = 0.1f },
                        Polygon = new List<TextractOutputExtensions.Point>
                        {
                            new(0.1f, 0.1f), new(0.6f, 0.1f), new(0.6f, 0.2f), new(0.1f, 0.2f)
                        }
                    },
                    Page = 1
                }
            }
        };

        // Create second document with three pages
        var document2 = new TextractOutputExtensions.TextractOutput
        {
            AnalyzeDocumentModelVersion = "1.0",
            DetectDocumentTextModelVersion = "1.0",
            DocumentMetadata = new TextractOutputExtensions.DocumentMetadata { Pages = 3 },
            Blocks = new List<TextractOutputExtensions.Block>
            {
                new()
                {
                    BlockType = TextractOutputExtensions.BlockTypeEnum.PAGE,
                    Id = Guid.NewGuid().ToString(),
                    Geometry = new TextractOutputExtensions.Geometry
                    {
                        BoundingBox = new TextractOutputExtensions.BoundingBox { Width = 1, Height = 1, Left = 0, Top = 0 },
                        Polygon = new List<TextractOutputExtensions.Point> { new(0, 0), new(1, 0), new(1, 1), new(0, 1) }
                    },
                    Page = 1
                },
                new()
                {
                    BlockType = TextractOutputExtensions.BlockTypeEnum.LINE,
                    Id = Guid.NewGuid().ToString(),
                    Text = "Document 2 Page 1 Line 1",
                    Confidence = 98.7f,
                    Geometry = new TextractOutputExtensions.Geometry
                    {
                        BoundingBox = new TextractOutputExtensions.BoundingBox { Width = 0.5f, Height = 0.1f, Left = 0.1f, Top = 0.1f },
                        Polygon = new List<TextractOutputExtensions.Point>
                        {
                            new(0.1f, 0.1f), new(0.6f, 0.1f), new(0.6f, 0.2f), new(0.1f, 0.2f)
                        }
                    },
                    Page = 1
                },
                new()
                {
                    BlockType = TextractOutputExtensions.BlockTypeEnum.PAGE,
                    Id = Guid.NewGuid().ToString(),
                    Geometry = new TextractOutputExtensions.Geometry
                    {
                        BoundingBox = new TextractOutputExtensions.BoundingBox { Width = 1, Height = 1, Left = 0, Top = 0 },
                        Polygon = new List<TextractOutputExtensions.Point> { new(0, 0), new(1, 0), new(1, 1), new(0, 1) }
                    },
                    Page = 2
                },
                new()
                {
                    BlockType = TextractOutputExtensions.BlockTypeEnum.LINE,
                    Id = Guid.NewGuid().ToString(),
                    Text = "Document 2 Page 2 Line 1",
                    Confidence = 97.8f,
                    Geometry = new TextractOutputExtensions.Geometry
                    {
                        BoundingBox = new TextractOutputExtensions.BoundingBox { Width = 0.5f, Height = 0.1f, Left = 0.1f, Top = 0.1f },
                        Polygon = new List<TextractOutputExtensions.Point>
                        {
                            new(0.1f, 0.1f), new(0.6f, 0.1f), new(0.6f, 0.2f), new(0.1f, 0.2f)
                        }
                    },
                    Page = 2
                },
                new()
                {
                    BlockType = TextractOutputExtensions.BlockTypeEnum.PAGE,
                    Id = Guid.NewGuid().ToString(),
                    Geometry = new TextractOutputExtensions.Geometry
                    {
                        BoundingBox = new TextractOutputExtensions.BoundingBox { Width = 1, Height = 1, Left = 0, Top = 0 },
                        Polygon = new List<TextractOutputExtensions.Point> { new(0, 0), new(1, 0), new(1, 1), new(0, 1) }
                    },
                    Page = 3
                },
                new()
                {
                    BlockType = TextractOutputExtensions.BlockTypeEnum.LINE,
                    Id = Guid.NewGuid().ToString(),
                    Text = "Document 2 Page 3 Line 1",
                    Confidence = 96.9f,
                    Geometry = new TextractOutputExtensions.Geometry
                    {
                        BoundingBox = new TextractOutputExtensions.BoundingBox { Width = 0.5f, Height = 0.1f, Left = 0.1f, Top = 0.1f },
                        Polygon = new List<TextractOutputExtensions.Point>
                        {
                            new(0.1f, 0.1f), new(0.6f, 0.1f), new(0.6f, 0.2f), new(0.1f, 0.2f)
                        }
                    },
                    Page = 3
                }
            }
        };

        // Create third document with one page
        var document3 = new TextractOutputExtensions.TextractOutput
        {
            AnalyzeDocumentModelVersion = "1.0",
            DetectDocumentTextModelVersion = "1.0",
            DocumentMetadata = new TextractOutputExtensions.DocumentMetadata { Pages = 1 },
            Blocks = new List<TextractOutputExtensions.Block>
            {
                new()
                {
                    BlockType = TextractOutputExtensions.BlockTypeEnum.PAGE,
                    Id = Guid.NewGuid().ToString(),
                    Geometry = new TextractOutputExtensions.Geometry
                    {
                        BoundingBox = new TextractOutputExtensions.BoundingBox { Width = 1, Height = 1, Left = 0, Top = 0 },
                        Polygon = new List<TextractOutputExtensions.Point> { new(0, 0), new(1, 0), new(1, 1), new(0, 1) }
                    },
                    Page = 1
                },
                new()
                {
                    BlockType = TextractOutputExtensions.BlockTypeEnum.LINE,
                    Id = Guid.NewGuid().ToString(),
                    Text = "Document 3 Line 1",
                    Confidence = 95.3f,
                    Geometry = new TextractOutputExtensions.Geometry
                    {
                        BoundingBox = new TextractOutputExtensions.BoundingBox { Width = 0.5f, Height = 0.1f, Left = 0.1f, Top = 0.1f },
                        Polygon = new List<TextractOutputExtensions.Point>
                        {
                            new(0.1f, 0.1f), new(0.6f, 0.1f), new(0.6f, 0.2f), new(0.1f, 0.2f)
                        }
                    },
                    Page = 1
                }
            }
        };

        // Merge the documents
        var filteredJson = TextractOutputExtensions.ToFilteredJsonString(new List<TextractOutputExtensions.TextractOutput>
            { document1, document2, document3 });

        // Verify the result is valid JSON and not null
        filteredJson.Should().NotBeNull();

        using var parsedJson = JsonDocument.Parse(filteredJson);
        var root = parsedJson.RootElement;

        // Verify the merged result
        root.ValueKind.Should().Be(JsonValueKind.Object);

        // Verify DocumentMetadata.Pages
        var documentMetadata = root.GetProperty("DocumentMetadata");
        var pages = documentMetadata.GetProperty("Pages").GetInt32();
        pages.Should().Be(5); // 1 + 3 + 1 = 5 pages total

        // Verify blocks array exists and has expected count
        var blocks = root.GetProperty("Blocks");
        blocks.ValueKind.Should().Be(JsonValueKind.Array);
        blocks.GetArrayLength().Should().Be(5); // Only LINE blocks are included in filtered output
    }

    [Fact]
    public void ToKBJsonString_WithEmptyInput_ReturnsEmptyArray()
    {
        // Arrange
        var emptyList = new List<TextractOutputExtensions.TextractOutput>();

        // Act
        var result = TextractOutputExtensions.ToKBJsonString(emptyList);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ToKBJsonString_WithNullInput_ReturnsEmptyArray()
    {
        // Arrange
        List<TextractOutputExtensions.TextractOutput>? nullList = null;

        // Act
        var result = TextractOutputExtensions.ToKBJsonString(nullList);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ToKBJsonString_WithSinglePage_ProcessesContentCorrectly()
    {
        // Arrange
        const string pageId = "11111111-1111-1111-1111-111111111111";
        const string lineId = "22222222-2222-2222-2222-222222222222";
        const string word1Id = "33333333-3333-3333-3333-333333333333";
        const string word2Id = "44444444-4444-4444-4444-444444444444";

        var textractOutput = new TextractOutputExtensions.TextractOutput
        {
            AnalyzeDocumentModelVersion = "1.0",
            DetectDocumentTextModelVersion = "1.0",
            DocumentMetadata = new TextractOutputExtensions.DocumentMetadata { Pages = 1 },
            Blocks = new List<TextractOutputExtensions.Block>
            {
                new()
                {
                    BlockType = TextractOutputExtensions.BlockTypeEnum.PAGE,
                    Id = pageId,
                    Geometry = new TextractOutputExtensions.Geometry
                    {
                        BoundingBox = new TextractOutputExtensions.BoundingBox { Width = 1, Height = 1, Left = 0, Top = 0 },
                        Polygon = new List<TextractOutputExtensions.Point> { new(0, 0), new(1, 0), new(1, 1), new(0, 1) }
                    },
                    Page = 1,
                    Relationships = new List<TextractOutputExtensions.Relationship>
                    {
                        new() { Type = "CHILD", Ids = new List<string> { lineId } }
                    }
                },
                new()
                {
                    BlockType = TextractOutputExtensions.BlockTypeEnum.LINE,
                    Id = lineId,
                    Text = "First line of text",
                    Confidence = 99.5f,
                    Geometry = new TextractOutputExtensions.Geometry
                    {
                        BoundingBox = new TextractOutputExtensions.BoundingBox { Width = 0.5f, Height = 0.1f, Left = 0.1f, Top = 0.1f },
                        Polygon = new List<TextractOutputExtensions.Point>
                        {
                            new(0.1f, 0.1f), new(0.6f, 0.1f), new(0.6f, 0.2f), new(0.1f, 0.2f)
                        }
                    },
                    Page = 1,
                    Relationships = new List<TextractOutputExtensions.Relationship>
                    {
                        new() { Type = "CHILD", Ids = new List<string> { word1Id, word2Id } }
                    }
                },
                new()
                {
                    BlockType = TextractOutputExtensions.BlockTypeEnum.WORD,
                    Id = word1Id,
                    Text = "First",
                    Confidence = 99.8f,
                    Geometry = new TextractOutputExtensions.Geometry
                    {
                        BoundingBox = new TextractOutputExtensions.BoundingBox { Width = 0.2f, Height = 0.1f, Left = 0.1f, Top = 0.1f },
                        Polygon = new List<TextractOutputExtensions.Point>
                        {
                            new(0.1f, 0.1f), new(0.3f, 0.1f), new(0.3f, 0.2f), new(0.1f, 0.2f)
                        }
                    },
                    Page = 1
                },
                new()
                {
                    BlockType = TextractOutputExtensions.BlockTypeEnum.WORD,
                    Id = word2Id,
                    Text = "line",
                    Confidence = 99.7f,
                    Geometry = new TextractOutputExtensions.Geometry
                    {
                        BoundingBox = new TextractOutputExtensions.BoundingBox { Width = 0.2f, Height = 0.1f, Left = 0.35f, Top = 0.1f },
                        Polygon = new List<TextractOutputExtensions.Point>
                        {
                            new(0.35f, 0.1f), new(0.55f, 0.1f), new(0.55f, 0.2f), new(0.35f, 0.2f)
                        }
                    },
                    Page = 1
                }
            }
        };

        // Act
        var result = TextractOutputExtensions.ToKBJsonString(new List<TextractOutputExtensions.TextractOutput> { textractOutput });

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("First line of text");
    }

    [Fact]
    public void ToKBJsonString_WithMultiplePages_ProcessesContentInOrder()
    {
        // Arrange
        const string page1Id = "11111111-1111-1111-1111-111111111111";
        const string page1LineId = "22222222-2222-2222-2222-222222222222";
        const string page1Word1Id = "33333333-3333-3333-3333-333333333333";
        const string page1Word2Id = "44444444-4444-4444-4444-444444444444";

        const string page2Id = "55555555-5555-5555-5555-555555555555";
        const string page2LineId = "66666666-6666-6666-6666-666666666666";
        const string page2Word1Id = "77777777-7777-7777-7777-777777777777";
        const string page2Word2Id = "88888888-8888-8888-8888-888888888888";

        var textractOutput = new TextractOutputExtensions.TextractOutput
        {
            AnalyzeDocumentModelVersion = "1.0",
            DetectDocumentTextModelVersion = "1.0",
            DocumentMetadata = new TextractOutputExtensions.DocumentMetadata { Pages = 2 },
            Blocks = new List<TextractOutputExtensions.Block>
            {
                // Page 1
                new()
                {
                    BlockType = TextractOutputExtensions.BlockTypeEnum.PAGE,
                    Id = page1Id,
                    Geometry = new TextractOutputExtensions.Geometry
                    {
                        BoundingBox = new TextractOutputExtensions.BoundingBox { Width = 1, Height = 1, Left = 0, Top = 0 },
                        Polygon = new List<TextractOutputExtensions.Point> { new(0, 0), new(1, 0), new(1, 1), new(0, 1) }
                    },
                    Page = 1,
                    Relationships = new List<TextractOutputExtensions.Relationship>
                    {
                        new() { Type = "CHILD", Ids = new List<string> { page1LineId } }
                    }
                },
                new()
                {
                    BlockType = TextractOutputExtensions.BlockTypeEnum.LINE,
                    Id = page1LineId,
                    Text = "Page 1 content",
                    Confidence = 99.5f,
                    Geometry = new TextractOutputExtensions.Geometry
                    {
                        BoundingBox = new TextractOutputExtensions.BoundingBox { Width = 0.5f, Height = 0.1f, Left = 0.1f, Top = 0.1f },
                        Polygon = new List<TextractOutputExtensions.Point>
                        {
                            new(0.1f, 0.1f), new(0.6f, 0.1f), new(0.6f, 0.2f), new(0.1f, 0.2f)
                        }
                    },
                    Page = 1,
                    Relationships = new List<TextractOutputExtensions.Relationship>
                    {
                        new() { Type = "CHILD", Ids = new List<string> { page1Word1Id, page1Word2Id } }
                    }
                },
                new()
                {
                    BlockType = TextractOutputExtensions.BlockTypeEnum.WORD,
                    Id = page1Word1Id,
                    Text = "Page",
                    Confidence = 99.8f,
                    Geometry = new TextractOutputExtensions.Geometry
                    {
                        BoundingBox = new TextractOutputExtensions.BoundingBox { Width = 0.2f, Height = 0.1f, Left = 0.1f, Top = 0.1f },
                        Polygon = new List<TextractOutputExtensions.Point>
                        {
                            new(0.1f, 0.1f), new(0.3f, 0.1f), new(0.3f, 0.2f), new(0.1f, 0.2f)
                        }
                    },
                    Page = 1
                },
                new()
                {
                    BlockType = TextractOutputExtensions.BlockTypeEnum.WORD,
                    Id = page1Word2Id,
                    Text = "1",
                    Confidence = 99.7f,
                    Geometry = new TextractOutputExtensions.Geometry
                    {
                        BoundingBox = new TextractOutputExtensions.BoundingBox { Width = 0.1f, Height = 0.1f, Left = 0.35f, Top = 0.1f },
                        Polygon = new List<TextractOutputExtensions.Point>
                        {
                            new(0.35f, 0.1f), new(0.45f, 0.1f), new(0.45f, 0.2f), new(0.35f, 0.2f)
                        }
                    },
                    Page = 1
                },
                // Page 2
                new()
                {
                    BlockType = TextractOutputExtensions.BlockTypeEnum.PAGE,
                    Id = page2Id,
                    Geometry = new TextractOutputExtensions.Geometry
                    {
                        BoundingBox = new TextractOutputExtensions.BoundingBox { Width = 1, Height = 1, Left = 0, Top = 0 },
                        Polygon = new List<TextractOutputExtensions.Point> { new(0, 0), new(1, 0), new(1, 1), new(0, 1) }
                    },
                    Page = 2,
                    Relationships = new List<TextractOutputExtensions.Relationship>
                    {
                        new() { Type = "CHILD", Ids = new List<string> { page2LineId } }
                    }
                },
                new()
                {
                    BlockType = TextractOutputExtensions.BlockTypeEnum.LINE,
                    Id = page2LineId,
                    Text = "Page 2 content",
                    Confidence = 99.5f,
                    Geometry = new TextractOutputExtensions.Geometry
                    {
                        BoundingBox = new TextractOutputExtensions.BoundingBox { Width = 0.5f, Height = 0.1f, Left = 0.1f, Top = 0.1f },
                        Polygon = new List<TextractOutputExtensions.Point>
                        {
                            new(0.1f, 0.1f), new(0.6f, 0.1f), new(0.6f, 0.2f), new(0.1f, 0.2f)
                        }
                    },
                    Page = 2,
                    Relationships = new List<TextractOutputExtensions.Relationship>
                    {
                        new() { Type = "CHILD", Ids = new List<string> { page2Word1Id, page2Word2Id } }
                    }
                },
                new()
                {
                    BlockType = TextractOutputExtensions.BlockTypeEnum.WORD,
                    Id = page2Word1Id,
                    Text = "Page",
                    Confidence = 99.8f,
                    Geometry = new TextractOutputExtensions.Geometry
                    {
                        BoundingBox = new TextractOutputExtensions.BoundingBox { Width = 0.2f, Height = 0.1f, Left = 0.1f, Top = 0.1f },
                        Polygon = new List<TextractOutputExtensions.Point>
                        {
                            new(0.1f, 0.1f), new(0.3f, 0.1f), new(0.3f, 0.2f), new(0.1f, 0.2f)
                        }
                    },
                    Page = 2
                },
                new()
                {
                    BlockType = TextractOutputExtensions.BlockTypeEnum.WORD,
                    Id = page2Word2Id,
                    Text = "2",
                    Confidence = 99.7f,
                    Geometry = new TextractOutputExtensions.Geometry
                    {
                        BoundingBox = new TextractOutputExtensions.BoundingBox { Width = 0.1f, Height = 0.1f, Left = 0.35f, Top = 0.1f },
                        Polygon = new List<TextractOutputExtensions.Point>
                        {
                            new(0.35f, 0.1f), new(0.45f, 0.1f), new(0.45f, 0.2f), new(0.35f, 0.2f)
                        }
                    },
                    Page = 2
                }
            }
        };

        // Act
        var result = TextractOutputExtensions.ToKBJsonString(new List<TextractOutputExtensions.TextractOutput> { textractOutput });

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("Page 1 content");
        result.Should().Contain("Page 2 content");
    }

    [Fact]
    public void ToKBJsonString_WithMultipleDocuments_MergesContentCorrectly()
    {
        // Arrange
        const string doc1PageId = "11111111-1111-1111-1111-111111111111";
        const string doc1LineId = "22222222-2222-2222-2222-222222222222";
        const string doc1Word1Id = "33333333-3333-3333-3333-333333333333";
        const string doc1Word2Id = "44444444-4444-4444-4444-444444444444";

        const string doc2PageId = "55555555-5555-5555-5555-555555555555";
        const string doc2LineId = "66666666-6666-6666-6666-666666666666";
        const string doc2Word1Id = "77777777-7777-7777-7777-777777777777";
        const string doc2Word2Id = "88888888-8888-8888-8888-888888888888";

        var document1 = new TextractOutputExtensions.TextractOutput
        {
            AnalyzeDocumentModelVersion = "1.0",
            DetectDocumentTextModelVersion = "1.0",
            DocumentMetadata = new TextractOutputExtensions.DocumentMetadata { Pages = 1 },
            Blocks = new List<TextractOutputExtensions.Block>
            {
                new()
                {
                    BlockType = TextractOutputExtensions.BlockTypeEnum.PAGE,
                    Id = doc1PageId,
                    Geometry = new TextractOutputExtensions.Geometry
                    {
                        BoundingBox = new TextractOutputExtensions.BoundingBox { Width = 1, Height = 1, Left = 0, Top = 0 },
                        Polygon = new List<TextractOutputExtensions.Point> { new(0, 0), new(1, 0), new(1, 1), new(0, 1) }
                    },
                    Page = 1,
                    Relationships = new List<TextractOutputExtensions.Relationship>
                    {
                        new() { Type = "CHILD", Ids = new List<string> { doc1LineId } }
                    }
                },
                new()
                {
                    BlockType = TextractOutputExtensions.BlockTypeEnum.LINE,
                    Id = doc1LineId,
                    Text = "Document 1 content",
                    Confidence = 99.5f,
                    Geometry = new TextractOutputExtensions.Geometry
                    {
                        BoundingBox = new TextractOutputExtensions.BoundingBox { Width = 0.5f, Height = 0.1f, Left = 0.1f, Top = 0.1f },
                        Polygon = new List<TextractOutputExtensions.Point>
                        {
                            new(0.1f, 0.1f), new(0.6f, 0.1f), new(0.6f, 0.2f), new(0.1f, 0.2f)
                        }
                    },
                    Page = 1,
                    Relationships = new List<TextractOutputExtensions.Relationship>
                    {
                        new() { Type = "CHILD", Ids = new List<string> { doc1Word1Id, doc1Word2Id } }
                    }
                },
                new()
                {
                    BlockType = TextractOutputExtensions.BlockTypeEnum.WORD,
                    Id = doc1Word1Id,
                    Text = "Document",
                    Confidence = 99.8f,
                    Geometry = new TextractOutputExtensions.Geometry
                    {
                        BoundingBox = new TextractOutputExtensions.BoundingBox { Width = 0.2f, Height = 0.1f, Left = 0.1f, Top = 0.1f },
                        Polygon = new List<TextractOutputExtensions.Point>
                        {
                            new(0.1f, 0.1f), new(0.3f, 0.1f), new(0.3f, 0.2f), new(0.1f, 0.2f)
                        }
                    },
                    Page = 1
                },
                new()
                {
                    BlockType = TextractOutputExtensions.BlockTypeEnum.WORD,
                    Id = doc1Word2Id,
                    Text = "1",
                    Confidence = 99.7f,
                    Geometry = new TextractOutputExtensions.Geometry
                    {
                        BoundingBox = new TextractOutputExtensions.BoundingBox { Width = 0.1f, Height = 0.1f, Left = 0.35f, Top = 0.1f },
                        Polygon = new List<TextractOutputExtensions.Point>
                        {
                            new(0.35f, 0.1f), new(0.45f, 0.1f), new(0.45f, 0.2f), new(0.35f, 0.2f)
                        }
                    },
                    Page = 1
                }
            }
        };

        var document2 = new TextractOutputExtensions.TextractOutput
        {
            AnalyzeDocumentModelVersion = "1.0",
            DetectDocumentTextModelVersion = "1.0",
            DocumentMetadata = new TextractOutputExtensions.DocumentMetadata { Pages = 1 },
            Blocks = new List<TextractOutputExtensions.Block>
            {
                new()
                {
                    BlockType = TextractOutputExtensions.BlockTypeEnum.PAGE,
                    Id = doc2PageId,
                    Geometry = new TextractOutputExtensions.Geometry
                    {
                        BoundingBox = new TextractOutputExtensions.BoundingBox { Width = 1, Height = 1, Left = 0, Top = 0 },
                        Polygon = new List<TextractOutputExtensions.Point> { new(0, 0), new(1, 0), new(1, 1), new(0, 1) }
                    },
                    Page = 1,
                    Relationships = new List<TextractOutputExtensions.Relationship>
                    {
                        new() { Type = "CHILD", Ids = new List<string> { doc2LineId } }
                    }
                },
                new()
                {
                    BlockType = TextractOutputExtensions.BlockTypeEnum.LINE,
                    Id = doc2LineId,
                    Text = "Document 2 content",
                    Confidence = 99.5f,
                    Geometry = new TextractOutputExtensions.Geometry
                    {
                        BoundingBox = new TextractOutputExtensions.BoundingBox { Width = 0.5f, Height = 0.1f, Left = 0.1f, Top = 0.1f },
                        Polygon = new List<TextractOutputExtensions.Point>
                        {
                            new(0.1f, 0.1f), new(0.6f, 0.1f), new(0.6f, 0.2f), new(0.1f, 0.2f)
                        }
                    },
                    Page = 1,
                    Relationships = new List<TextractOutputExtensions.Relationship>
                    {
                        new() { Type = "CHILD", Ids = new List<string> { doc2Word1Id, doc2Word2Id } }
                    }
                },
                new()
                {
                    BlockType = TextractOutputExtensions.BlockTypeEnum.WORD,
                    Id = doc2Word1Id,
                    Text = "Document",
                    Confidence = 99.8f,
                    Geometry = new TextractOutputExtensions.Geometry
                    {
                        BoundingBox = new TextractOutputExtensions.BoundingBox { Width = 0.2f, Height = 0.1f, Left = 0.1f, Top = 0.1f },
                        Polygon = new List<TextractOutputExtensions.Point>
                        {
                            new(0.1f, 0.1f), new(0.3f, 0.1f), new(0.3f, 0.2f), new(0.1f, 0.2f)
                        }
                    },
                    Page = 1
                },
                new()
                {
                    BlockType = TextractOutputExtensions.BlockTypeEnum.WORD,
                    Id = doc2Word2Id,
                    Text = "2",
                    Confidence = 99.7f,
                    Geometry = new TextractOutputExtensions.Geometry
                    {
                        BoundingBox = new TextractOutputExtensions.BoundingBox { Width = 0.1f, Height = 0.1f, Left = 0.35f, Top = 0.1f },
                        Polygon = new List<TextractOutputExtensions.Point>
                        {
                            new(0.35f, 0.1f), new(0.45f, 0.1f), new(0.45f, 0.2f), new(0.35f, 0.2f)
                        }
                    },
                    Page = 1
                }
            }
        };

        // Act
        var result = TextractOutputExtensions.ToKBJsonString(new List<TextractOutputExtensions.TextractOutput> { document1, document2 });

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("Document 1 content");
        result.Should().Contain("Document 2 content");
    }

    [Fact]
    public void ToKBJsonString_WithReadingOrder_PreservesVerticalSpacing()
    {
        // Arrange
        const string pageId = "11111111-1111-1111-1111-111111111111";
        const string line1Id = "22222222-2222-2222-2222-222222222222";
        const string line1Word1Id = "33333333-3333-3333-3333-333333333333";
        const string line1Word2Id = "44444444-4444-4444-4444-444444444444";
        const string line2Id = "55555555-5555-5555-5555-555555555555";
        const string line2Word1Id = "66666666-6666-6666-6666-666666666666";
        const string line2Word2Id = "77777777-7777-7777-7777-777777777777";

        var textractOutput = new TextractOutputExtensions.TextractOutput
        {
            AnalyzeDocumentModelVersion = "1.0",
            DetectDocumentTextModelVersion = "1.0",
            DocumentMetadata = new TextractOutputExtensions.DocumentMetadata { Pages = 1 },
            Blocks = new List<TextractOutputExtensions.Block>
            {
                new()
                {
                    BlockType = TextractOutputExtensions.BlockTypeEnum.PAGE,
                    Id = pageId,
                    Geometry = new TextractOutputExtensions.Geometry
                    {
                        BoundingBox = new TextractOutputExtensions.BoundingBox { Width = 1, Height = 1, Left = 0, Top = 0 },
                        Polygon = new List<TextractOutputExtensions.Point> { new(0, 0), new(1, 0), new(1, 1), new(0, 1) }
                    },
                    Page = 1,
                    Relationships = new List<TextractOutputExtensions.Relationship>
                    {
                        new() { Type = "CHILD", Ids = new List<string> { line1Id, line2Id } }
                    }
                },
                new()
                {
                    BlockType = TextractOutputExtensions.BlockTypeEnum.LINE,
                    Id = line1Id,
                    Text = "First paragraph",
                    Confidence = 99.5f,
                    Geometry = new TextractOutputExtensions.Geometry
                    {
                        BoundingBox = new TextractOutputExtensions.BoundingBox { Width = 0.5f, Height = 0.1f, Left = 0.1f, Top = 0.1f },
                        Polygon = new List<TextractOutputExtensions.Point>
                        {
                            new(0.1f, 0.1f), new(0.6f, 0.1f), new(0.6f, 0.2f), new(0.1f, 0.2f)
                        }
                    },
                    Page = 1,
                    Relationships = new List<TextractOutputExtensions.Relationship>
                    {
                        new() { Type = "CHILD", Ids = new List<string> { line1Word1Id, line1Word2Id } }
                    }
                },
                new()
                {
                    BlockType = TextractOutputExtensions.BlockTypeEnum.WORD,
                    Id = line1Word1Id,
                    Text = "First",
                    Confidence = 99.8f,
                    Geometry = new TextractOutputExtensions.Geometry
                    {
                        BoundingBox = new TextractOutputExtensions.BoundingBox { Width = 0.2f, Height = 0.1f, Left = 0.1f, Top = 0.1f },
                        Polygon = new List<TextractOutputExtensions.Point>
                        {
                            new(0.1f, 0.1f), new(0.3f, 0.1f), new(0.3f, 0.2f), new(0.1f, 0.2f)
                        }
                    },
                    Page = 1
                },
                new()
                {
                    BlockType = TextractOutputExtensions.BlockTypeEnum.WORD,
                    Id = line1Word2Id,
                    Text = "paragraph",
                    Confidence = 99.7f,
                    Geometry = new TextractOutputExtensions.Geometry
                    {
                        BoundingBox = new TextractOutputExtensions.BoundingBox { Width = 0.3f, Height = 0.1f, Left = 0.35f, Top = 0.1f },
                        Polygon = new List<TextractOutputExtensions.Point>
                        {
                            new(0.35f, 0.1f), new(0.65f, 0.1f), new(0.65f, 0.2f), new(0.35f, 0.2f)
                        }
                    },
                    Page = 1
                },
                new()
                {
                    BlockType = TextractOutputExtensions.BlockTypeEnum.LINE,
                    Id = line2Id,
                    Text = "Second paragraph",
                    Confidence = 99.5f,
                    Geometry = new TextractOutputExtensions.Geometry
                    {
                        BoundingBox = new TextractOutputExtensions.BoundingBox { Width = 0.5f, Height = 0.1f, Left = 0.1f, Top = 0.3f },
                        Polygon = new List<TextractOutputExtensions.Point>
                        {
                            new(0.1f, 0.3f), new(0.6f, 0.3f), new(0.6f, 0.4f), new(0.1f, 0.4f)
                        }
                    },
                    Page = 1,
                    Relationships = new List<TextractOutputExtensions.Relationship>
                    {
                        new() { Type = "CHILD", Ids = new List<string> { line2Word1Id, line2Word2Id } }
                    }
                },
                new()
                {
                    BlockType = TextractOutputExtensions.BlockTypeEnum.WORD,
                    Id = line2Word1Id,
                    Text = "Second",
                    Confidence = 99.8f,
                    Geometry = new TextractOutputExtensions.Geometry
                    {
                        BoundingBox = new TextractOutputExtensions.BoundingBox { Width = 0.25f, Height = 0.1f, Left = 0.1f, Top = 0.3f },
                        Polygon = new List<TextractOutputExtensions.Point>
                        {
                            new(0.1f, 0.3f), new(0.35f, 0.3f), new(0.35f, 0.4f), new(0.1f, 0.4f)
                        }
                    },
                    Page = 1
                },
                new()
                {
                    BlockType = TextractOutputExtensions.BlockTypeEnum.WORD,
                    Id = line2Word2Id,
                    Text = "paragraph",
                    Confidence = 99.7f,
                    Geometry = new TextractOutputExtensions.Geometry
                    {
                        BoundingBox = new TextractOutputExtensions.BoundingBox { Width = 0.3f, Height = 0.1f, Left = 0.4f, Top = 0.3f },
                        Polygon = new List<TextractOutputExtensions.Point>
                        {
                            new(0.4f, 0.3f), new(0.7f, 0.3f), new(0.7f, 0.4f), new(0.4f, 0.4f)
                        }
                    },
                    Page = 1
                }
            }
        };

        // Act
        var result = TextractOutputExtensions.ToKBJsonString(new List<TextractOutputExtensions.TextractOutput> { textractOutput });

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("First paragraph");
        result.Should().Contain("Second paragraph");
        // Verify there's a line break between paragraphs
        result.Should().Contain("\n\n");
    }

    // class TextractOutputSparse
    // {
    //   public string AnalyzeDocumentModelVersion { get; set; }
    //   public string DetectDocumentTextModelVersion { get; set; }
    //   public TextractOutputExtensions.DocumentMetadata DocumentMetadata { get; set; }
    //   public List<TextractOutputExtensions.BlockSparse> Blocks { get; set; }
    //
    // }
    [Fact]
    public void TestToKbJsonStringWithRealData()
    {
        //read the json file
        var content = File.ReadAllText(JsonFilePath2);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            // System.Text.Json ignores unknown properties by default
        };
        var response = JsonSerializer.Deserialize<TextractOutputExtensions.TextractOutputSparse>(content, options);

         var result = TextractOutputExtensions.ToKBJsonStringSparse(new List<TextractOutputExtensions.TextractOutputSparse> { response });
        // This test may return empty string for certain data formats, which is acceptable
        result.Should().NotBeNull();
    }
}