using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;



namespace ThirdOpinion.Common.Textract.Models;

public class FloatFormatConverter : JsonConverter<float>
{
    private readonly int _digits;

    public FloatFormatConverter(int digits)
    {
        _digits = digits;
    }

    public override float Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.GetSingle();
    }

    public override void Write(Utf8JsonWriter writer, float value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue((float)Math.Round(value, _digits));
    }
}

public class DoubleFormatConverter : JsonConverter<double>
{
    private readonly int _digits;

    public DoubleFormatConverter(int digits)
    {
        _digits = digits;
    }

    public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.GetDouble();
    }

    public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(Math.Round(value, _digits));
    }
}

public static class TextractOutputExtensions
{
    /// <summary>
    /// Converts a list of TextractOutput objects to a filtered JSON string. Remove all blocks that are not LINE or WORD to reduce the size of the JSON.
    /// </summary>
    /// <param name="textractOutput">The list of TextractOutput objects to convert</param>
    /// <returns>A filtered JSON string</returns>
    public static string? ToFilteredJsonString(List<TextractOutput> textractOutput)
    {
        if (textractOutput == null || textractOutput.Count == 0) return null;

        TextractOutput mergedOutput;
        if (textractOutput.Count == 1) mergedOutput = textractOutput[0];

        mergedOutput = new TextractOutput
        {
            AnalyzeDocumentModelVersion = textractOutput[0].AnalyzeDocumentModelVersion,
            DetectDocumentTextModelVersion = textractOutput[0].DetectDocumentTextModelVersion,
            DocumentMetadata = new DocumentMetadata
            {
                Pages = textractOutput.Sum(output => output.DocumentMetadata?.Pages ?? 0)
            },
            Blocks = new List<Block>(),
            Pages = textractOutput.Sum(output => output.DocumentMetadata?.Pages ?? 0)
        };

        var pageOffset = 0;

        foreach (var output in textractOutput)
        {
            if (output.Blocks != null)
                foreach (var block in output.Blocks)
                {
                    if (block.Page.HasValue) block.Page = block.Page.Value + pageOffset;

                    mergedOutput.Blocks.Add(block);
                }

            pageOffset += output.DocumentMetadata?.Pages ?? 0;
        }

        var filteredBlocks = mergedOutput.Blocks?
            .Where(b => b.BlockType == BlockTypeEnum.LINE || b.BlockType == BlockTypeEnum.WORD)
            .Select(b => new FilteredBlock
            {
                BlockType = b.BlockType,
                Confidence = b.Confidence,
                Text = b.Text,
                Geometry = b.Geometry?.Polygon ?? null,
                Id = b.Id,
                Relationships = b.Relationships ?? null,
                Page = b.Page
            }).ToList();


        var filteredOutput = new
        {
            mergedOutput.AnalyzeDocumentModelVersion,
            mergedOutput.DetectDocumentTextModelVersion,
            mergedOutput.DocumentMetadata,
            Blocks = filteredBlocks
        };

        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
            Converters = { new FloatFormatConverter(4), new DoubleFormatConverter(4) }
        };
        return JsonSerializer.Serialize(filteredOutput, options);
    }


    /// <summary>
    /// Converts a list of TextractOutput objects to a filtered JSON string. Remove all blocks that are not LINE or WORD to reduce the size of the JSON.
    /// </summary>
    /// <param name="textractOutput">The list of TextractOutput objects to convert</param>
    /// <returns>A filtered JSON string</returns>
    public static string? ToFilteredJsonStringv2(List<TextractOutput> textractOutput)
    {
        if (textractOutput == null || textractOutput.Count == 0) return null;

        TextractOutput mergedOutput;
        if (textractOutput.Count == 1) mergedOutput = textractOutput[0];

        mergedOutput = new TextractOutput
        {
            AnalyzeDocumentModelVersion = textractOutput[0].AnalyzeDocumentModelVersion,
            DetectDocumentTextModelVersion = textractOutput[0].DetectDocumentTextModelVersion,
            DocumentMetadata = new DocumentMetadata
            {
                Pages = textractOutput.Sum(output => output.DocumentMetadata?.Pages ?? 0)
            },
            Blocks = new List<Block>(),
            Pages = textractOutput.Sum(output => output.DocumentMetadata?.Pages ?? 0)
        };

        var pageOffset = 0;

        foreach (var output in textractOutput)
        {
            if (output.Blocks != null)
                foreach (var block in output.Blocks)
                {
                    if (block.Page.HasValue) block.Page = block.Page.Value + pageOffset;

                    mergedOutput.Blocks.Add(block);
                }

            pageOffset += output.DocumentMetadata?.Pages ?? 0;
        }

        var filteredBlocks = mergedOutput.Blocks?
            .Where(b => b.BlockType == BlockTypeEnum.LINE || b.BlockType == BlockTypeEnum.WORD || b.BlockType == BlockTypeEnum.PAGE)
            .Select(b => new FilteredBlock
            {
                BlockType = b.BlockType,
                Confidence = b.Confidence,
                Text = b.Text,
                Geometry = SortTextractPolygonPointsClockwise(b.Geometry?.Polygon) ?? null,
                Id = b.Id,
                Relationships = b.Relationships ?? null,
                Page = b.Page
            }).ToList();


        filteredBlocks = filteredBlocks?.OrderBy(b => b.Page)
            .ThenBy(b => b.Geometry?.First().Y)
            .ThenBy(b => b.Geometry?.First().X)
            .ToList();

        var filteredOutput = new
        {
            mergedOutput.AnalyzeDocumentModelVersion,
            mergedOutput.DetectDocumentTextModelVersion,
            mergedOutput.DocumentMetadata,
            Blocks = filteredBlocks
        };

        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
            Converters = { new FloatFormatConverter(4), new DoubleFormatConverter(4) }
        };
        return JsonSerializer.Serialize(filteredOutput, options);
    }


    public static JsonDocument ToFilteredJson(List<TextractOutput> textractOutput)
    {
        var filteredOutput = ToFilteredJsonString(textractOutput);
        if (filteredOutput == null) return JsonDocument.Parse("{}");
        return JsonDocument.Parse(filteredOutput);
    }

    public static JsonDocument ToFilteredJsonv2(List<TextractOutput> textractOutput)
    {
        var filteredOutput = ToFilteredJsonStringv2(textractOutput);
        if (filteredOutput == null) return JsonDocument.Parse("{}");
        return JsonDocument.Parse(filteredOutput);
    }

    public static string? ToKBJsonString(List<TextractOutput> textractOutput)
    {
        FilteredTextractOutput filteredOutput = ToFilteredTextractObjectv2(textractOutput);

        if (filteredOutput == null || filteredOutput.Blocks == null) return null;

        var blocksById = new Dictionary<string, FilteredBlock>();
        foreach (var block in filteredOutput.Blocks)
        {
            blocksById.Add(block.Id, block);
        }

        var result = new StringBuilder();

        ProcessPageContent(filteredOutput.DocumentMetadata.Pages, blocksById, result);
        return result.ToString();
    }

    public static string? ToKBJsonStringSparse(List<TextractOutputSparse> textractOutput)
    {
        List<TextractOutput> input = new List<TextractOutput>();

        foreach (var textractOutputSparse in textractOutput)
        {
            var blocks = textractOutputSparse.Blocks?.Select(b => new Block
            {
                BlockType = b.BlockType,
                Confidence = b.Confidence,
                Text = b.Text,
                Geometry = new Geometry
                {
                    BoundingBox = null,
                    Polygon = b.Geometry.Select(p => new Point(p.X, p.Y)).ToList()
                },
                Id = b.Id,
                Relationships = b.Relationships,
                RowIndex = b.RowIndex,
                ColumnIndex = b.ColumnIndex,
                RowSpan = b.RowSpan,
                ColumnSpan = b.ColumnSpan,
                SelectionStatus = b.SelectionStatus,
                EntityTypes = b.EntityTypes,
                Page = b.Page
            }).ToList();

            input.Add(new TextractOutput
            {
                Blocks = blocks,
                AnalyzeDocumentModelVersion = textractOutputSparse.AnalyzeDocumentModelVersion,
                DetectDocumentTextModelVersion = textractOutputSparse.DetectDocumentTextModelVersion,
            });
        }

        return ToKBJsonString(input);
    }

    public static FilteredTextractOutput? ToFilteredTextractObject(List<TextractOutput> textractOutput)
    {
        var filteredOutput = ToFilteredJsonString(textractOutput);
        if (filteredOutput == null) return null;
        return JsonSerializer.Deserialize<FilteredTextractOutput>(filteredOutput);
    }

    public static FilteredTextractOutput? ToFilteredTextractObjectv2(List<TextractOutput> textractOutput)
    {
        var filteredOutput = ToFilteredJsonStringv2(textractOutput);
        if (filteredOutput == null) return null;
        return JsonSerializer.Deserialize<FilteredTextractOutput>(filteredOutput);
    }

    /// <summary>
    /// Sorts Textract polygon points in clockwise order, starting from the upper-left point.
    /// Maybe they are already sorted?
    /// </summary>
    /// <param name="points">The list of Textract points forming the polygon</param>
    /// <returns>A new list of points sorted in clockwise order</returns>
    public static List<Point>? SortTextractPolygonPointsClockwise(
        List<Point> points)
    {
        if (points == null || points.Count == 0) return null;

        if (points == null || points.Count <= 3)
            return new List<Point>(points);

        // Find the centroid (average of all points)
        double centerX = points.Average(p => p.X);
        double centerY = points.Average(p => p.Y);

        // Find the upper-left point (minimum Y and X)
        var upperLeft = points.OrderBy(p => p.Y).ThenBy(p => p.X).First();

        // Sort points clockwise around the centroid, starting from the upper-left
        var sortedPoints = new List<Point>(points);
        sortedPoints.Sort((a, b) =>
        {
            // If the current point is the upper-left point, it comes first
            if (a.X == upperLeft.X && a.Y == upperLeft.Y) return -1;
            if (b.X == upperLeft.X && b.Y == upperLeft.Y) return 1;

            // Calculate the angles from the centroid to each point
            double angleA = Math.Atan2(a.Y - centerY, a.X - centerX);
            double angleB = Math.Atan2(b.Y - centerY, b.X - centerX);

            // Adjust angles to start from the upper-left
            double referenceAngle = Math.Atan2(upperLeft.Y - centerY, upperLeft.X - centerX);

            // Make angles relative to the reference angle
            angleA = (angleA - referenceAngle + 2 * Math.PI) % (2 * Math.PI);
            angleB = (angleB - referenceAngle + 2 * Math.PI) % (2 * Math.PI);

            // Sort by angle
            return angleA.CompareTo(angleB);
        });

        return sortedPoints;
    }


    private static void ProcessPageContent(int pages, Dictionary<string, FilteredBlock> blocksById,
     StringBuilder result)
    {
        // Get all LINE and WORD blocks that are direct children of this page
        // we need to keep the blocks in reading order

        //Get all the page blocks
        var pageBlocks = blocksById.Values.Where(b => b.BlockType == BlockTypeEnum.PAGE).ToList();

        //Get all the blocks that are direct children of the page blocks
        var childBlocks = blocksById.Values.Where(b => b.Relationships != null).ToList();

        foreach (var pageBlock in pageBlocks)
        {
            var contentBlocks = new List<FilteredBlock>();

            //
                if (pageBlock.Relationships != null)
                {
                    //assume the children are in reading order?
                    foreach (var relationship in pageBlock.Relationships)
                    {
                        if (relationship.Type == "CHILD")
                        {
                            foreach (var blockId in relationship.Ids)
                            {
                                if (blocksById.TryGetValue(blockId, out var foundBlock))
                                {
                                    // Include LINE blocks for processing in reading order
                                    if (foundBlock.BlockType == BlockTypeEnum.LINE)
                                    {
                                        contentBlocks.Add(foundBlock);
                                    }
                                    // Some documents might return WORD blocks directly instead of LINES
                                    else if (foundBlock.BlockType == BlockTypeEnum.WORD)
                                    {
                                        contentBlocks.Add(foundBlock);
                                    }
                                }
                            }
                        }
                }
            }

            if(pageBlock.Page > 1)
                result.AppendLine();

            result.AppendLine($"[meta:page={pageBlock.Page}]");

                // The polygon is sorted upper left, clockwise so not needed?
                // contentBlocks = contentBlocks.OrderBy(b => b.Geometry?.First().Y)
                //                             .ThenBy(b => b.Geometry?.First().X)
                //                         .ToList()
                // Process content blocks in order
                for (int i = 0; i < contentBlocks.Count; i++)
                {
                    FilteredBlock block = contentBlocks[i];

                    // Regular text content
                    result.AppendLine(GetBlockText(block, blocksById));

                    // Add paragraph breaks between distinct blocks of text
                    if (i < contentBlocks.Count - 1)
                    {
                        var nextBlock = contentBlocks[i + 1];

                        // If the vertical distance to the next block is significant, add a paragraph break
                        if (nextBlock.Geometry?.First().Y - block.Geometry?.First().Y > 0.02)
                        {
                            result.AppendLine();
                        }
                }
            } //for each page
        }
    }

    private static string ValuesWithMetaData(FilteredBlock block)
    {
        return $"{block.Text ?? string.Empty} [meta:id={block.Id}]";
    }

    // Helper method to extract text from a block
    private static string GetBlockText(FilteredBlock block, Dictionary<string, FilteredBlock> blocksById)
    {
        // If the block has direct text, use it
        if (!string.IsNullOrEmpty(block.Text))
        {
            return ValuesWithMetaData(block);
        }

        // Otherwise, extract text from child blocks
        var text = new StringBuilder();

        if (block.Relationships != null)
        {
            foreach (var relationship in block.Relationships)
            {
                if (relationship.Type == "CHILD")
                {
                    foreach (var childId in relationship.Ids)
                    {
                        if (blocksById.TryGetValue(childId, out var childBlock))
                        {
                            if (!string.IsNullOrEmpty(childBlock.Text))
                            {
                                if (text.Length > 0)
                                    text.Append(" ");
                                text.Append(ValuesWithMetaData(block));
                            }
                        }
                    }
                }
            }
        }

        return text.ToString();
    }

    private static string GetCellContent(FilteredBlock cell, Dictionary<string, FilteredBlock> blocksById)
    {
        var content = new StringBuilder();

        // If the cell has a direct text value, use it
        if (!string.IsNullOrEmpty(cell.Text))
        {
            return cell.Text;
        }

        // Otherwise, we need to find the child word blocks
        if (cell.Relationships != null)
        {
            foreach (var relationship in cell.Relationships)
            {
                if (relationship.Type == "CHILD")
                {
                    foreach (var wordId in relationship.Ids)
                    {
                        if (blocksById.TryGetValue(wordId, out var wordBlock) &&
                            (wordBlock.BlockType == BlockTypeEnum.WORD || wordBlock.BlockType == BlockTypeEnum.LINE))
                        {
                            if (content.Length > 0)
                                content.Append(" ");

                            content.Append(wordBlock.Text);
                        }
                    }
                }
            }
        }

        return content.ToString();
    }

    public class TextractOutput
    {
        public List<Block>? Blocks { get; set; }
        public string? AnalyzeDocumentModelVersion { get; set; }
        public string? DetectDocumentTextModelVersion { get; set; }
        public DocumentMetadata? DocumentMetadata { get; set; }

        public int Pages { get; set; }
        public string? DetectDocumentModelVersion { get; set; }


        public static TextractOutput FromAmazonTextractResponse(Amazon.Textract.Model.DetectDocumentTextResponse response,
            bool noGeo = false, bool noRelationships = false)
        {
            var t = new Amazon.Textract.Model.GetDocumentTextDetectionResponse
            {
                DetectDocumentTextModelVersion = response.DetectDocumentTextModelVersion,
                DocumentMetadata = response.DocumentMetadata,
                Blocks = response.Blocks,
            };

            Console.WriteLine($"Converted DetectDocumentTextResponse to GetDocumentTextDetectionResponse for TextractOutput.FromAmazonTextractResponse");

            return FromAmazonTextractResponse(t, noGeo, noRelationships);
        }

        public static TextractOutput FromAmazonTextractResponse(Amazon.Textract.Model.GetDocumentTextDetectionResponse response,
            bool noGeo = false, bool noRelationships = false)
        {
            // Calculate page count from blocks since DocumentMetadata.Pages may not be available
            var pageCount = 0;
            if (response.Blocks != null)
            {
                foreach (var block in response.Blocks)
                {
                    if (block.BlockType == "PAGE")
                    {
                        pageCount++;
                    }
                }
            }

           // Convert blocks without LINQ
            List<Block>? blocks = null;
            if (response.Blocks != null)
            {
                blocks = new List<Block>();
                foreach (Amazon.Textract.Model.Block? b in response.Blocks)
                {
                    // Convert polygon points without LINQ
                    List<Point>? polygon = null;
                    if (!noGeo && b.Geometry?.Polygon != null)
                    {
                        polygon = new List<Point>();
                        foreach (Amazon.Textract.Model.Point? p in b.Geometry.Polygon)
                        {
                            polygon.Add(new Point(p.X, p.Y));
                        }
                    }

                    // Convert relationships without LINQ
                    List<Relationship>? relationships = null;
                    if (!noRelationships && b.Relationships != null)
                    {
                        relationships = new List<Relationship>();
                        foreach (Amazon.Textract.Model.Relationship? r in b.Relationships)
                        {
                            var ids = new List<string>();
                            if (r.Ids != null)
                            {
                                foreach (var id in r.Ids)
                                {
                                    ids.Add(id);
                                }
                            }

                            relationships.Add(new Relationship
                            {
                                Type = r.Type,
                                Ids = ids
                            });
                        }
                    }

                    // Convert entity types without LINQ
                    List<string>? entityTypes = null;
                    if (b.EntityTypes != null)
                    {
                        entityTypes = new List<string>();
                        foreach (string? entityType in b.EntityTypes)
                        {
                            entityTypes.Add(entityType);
                        }
                    }

                    // blocks.Add(new Block
                    // {
                    //     BlockType = Enum.Parse<BlockTypeEnum>(b.BlockType),
                    //     Confidence = b.Confidence,
                    //     Text = b.Text,
                    //     TextType = b.TextType,
                    //     Geometry = noGeo
                    //         ? null
                    //         : new Geometry
                    //         {
                    //             BoundingBox = new BoundingBox
                    //             {
                    //                 Height = b.Geometry.BoundingBox.Height,
                    //                 Left = b.Geometry.BoundingBox.Left,
                    //                 Top = b.Geometry.BoundingBox.Top,
                    //                 Width = b.Geometry.BoundingBox.Width
                    //             },
                    //             Polygon = polygon
                    //         },
                    //     Id = b.Id,
                    //     Relationships = relationships,
                    //     Page = b.Page,
                    //     RowIndex = b.RowIndex,
                    //     ColumnIndex = b.ColumnIndex,
                    //     RowSpan = b.RowSpan,
                    //     ColumnSpan = b.ColumnSpan,
                    //     SelectionStatus = b.SelectionStatus,
                    //     EntityTypes = entityTypes
                    // });
                }
            }

            var output = new TextractOutput
            {
                AnalyzeDocumentModelVersion = response.DetectDocumentTextModelVersion,
                DetectDocumentTextModelVersion = response.DetectDocumentTextModelVersion,
                Pages = pageCount,
                DocumentMetadata = response.DocumentMetadata != null
                    ? new DocumentMetadata
                    {
                        Pages = pageCount
                    }
                    : null,
                Blocks = new List<Block>() //blocks
            };

            return output;
        }
    }

     public class TextractOutputSparse
    {
        public List<BlockSparse>? Blocks { get; set; }
        public string? AnalyzeDocumentModelVersion { get; set; }
        public string? DetectDocumentTextModelVersion { get; set; }
        public DocumentMetadata? DocumentMetadata { get; set; }
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum BlockTypeEnum
    {
        PAGE,
        LINE,
        WORD,
        TABLE,
        CELL,
        LAYOUT_TEXT,
        QUERY_RESULT,
        MERGED_CELL,
        SELECTION_ELEMENT,
        KEY_VALUE_SET,
        TABLE_ELEMENT,
        LAYOUT_ELEMENT,
        TABLE_TITLE,
        QUERY,
        LAYOUT_TITLE,
        LAYOUT_KEY_VALUE,
        LAYOUT_FIGURE
    }

    //Perhaps refactor this to be typed by block type
    public class Block
    {
        public required BlockTypeEnum BlockType { get; set; }
        public float? Confidence { get; set; }
        public string? Text { get; set; }
        public string? TextType { get; set; }
        public required Geometry Geometry { get; set; }
        public required string Id { get; set; }
        public List<Relationship>? Relationships { get; set; }
        public int? RowIndex { get; set; }
        public int? ColumnIndex { get; set; }
        public int? RowSpan { get; set; }
        public int? ColumnSpan { get; set; }
        public string? SelectionStatus { get; set; }
        public List<string>? EntityTypes { get; set; }
        public int? Page { get; set; }
    }

    public class BlockSparse
    {
        public required BlockTypeEnum BlockType { get; set; }
        public float? Confidence { get; set; }
        public string? Text { get; set; }
        public string? TextType { get; set; }
        public required Point[] Geometry { get; set; }
        public required string Id { get; set; }
        public List<Relationship>? Relationships { get; set; }
        public int? RowIndex { get; set; }
        public int? ColumnIndex { get; set; }
        public int? RowSpan { get; set; }
        public int? ColumnSpan { get; set; }
        public string? SelectionStatus { get; set; }
        public List<string>? EntityTypes { get; set; }
        public int? Page { get; set; }
    }

    public class Geometry
    {
        public required BoundingBox BoundingBox { get; set; }
        public required List<Point> Polygon { get; set; }
    }

    public class BoundingBox
    {
        public float Width { get; set; }
        public float Height { get; set; }
        public float Left { get; set; }
        public float Top { get; set; }
    }

    public class Point
    {
        public Point(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float X { get; set; }
        public float Y { get; set; }
    }

    public class Relationship
    {
        public required string Type { get; set; }
        public required List<string> Ids { get; set; }
    }

    public class DocumentMetadata
    {
        public int Pages { get; set; }
    }

    public class FilteredTextractOutput
    {
        public string? AnalyzeDocumentModelVersion { get; set; }
        public string? DetectDocumentModelVersion { get; set; }
        public DocumentMetadata? DocumentMetadata { get; set; }
        public List<FilteredBlock>? Blocks { get; set; }
    }

    public class FilteredBlock
    {
        public required BlockTypeEnum BlockType { get; set; }
        public float? Confidence { get; set; }
        public string? Text { get; set; }
        public List<Point>? Geometry { get; set; }
        public required string Id { get; set; }
        public List<Relationship>? Relationships { get; set; }
        public int? Page { get; set; }
    }
}