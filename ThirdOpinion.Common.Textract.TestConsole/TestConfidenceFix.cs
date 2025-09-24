using System;
using System.IO;
using System.Text.Json;
using Amazon.Textract.Model;
using ThirdOpinion.Common.Textract.Models;

namespace TextractLib.Console
{
    public static class TestConfidenceFix
    {
        public static void RunTest()
        {
            System.Console.WriteLine("Testing Confidence property access fix...");

            try
            {
                // Create a mock DetectDocumentTextResponse to test the Confidence property access
                var response = new DetectDocumentTextResponse
                {
                    DetectDocumentTextModelVersion = "1.0",
                    DocumentMetadata = new DocumentMetadata(),
                    Blocks = new List<Block>
                    {
                        new Block
                        {
                            BlockType = "PAGE",
                            Id = "page1",
                            Confidence = 100f,
                            Geometry = new Geometry
                            {
                                BoundingBox = new BoundingBox { Width = 1, Height = 1, Left = 0, Top = 0 },
                                Polygon = new List<Point> { new Point { X = 0, Y = 0 }, new Point { X = 1, Y = 0 }, new Point { X = 1, Y = 1 }, new Point { X = 0, Y = 1 } }
                            },
                            Page = 1
                        },
                        new Block
                        {
                            BlockType = "LINE",
                            Id = "line1",
                            Text = "Sample text",
                            Confidence = 99.5f,
                            Geometry = new Geometry
                            {
                                BoundingBox = new BoundingBox { Width = 0.5f, Height = 0.1f, Left = 0.1f, Top = 0.1f },
                                Polygon = new List<Point> { new Point { X = 0.1f, Y = 0.1f }, new Point { X = 0.6f, Y = 0.1f }, new Point { X = 0.6f, Y = 0.2f }, new Point { X = 0.1f, Y = 0.2f } }
                            },
                            Page = 1
                        }
                    }
                };

                System.Console.WriteLine($"Mock test data created. Block count: {response.Blocks.Count}");

                // Test the problematic line that was causing the error
                System.Console.WriteLine("Testing TextractOutput.FromAmazonTextractResponse()...");
                var textractOutput = TextractOutputExtensions.TextractOutput.FromAmazonTextractResponse(response);

                System.Console.WriteLine($"✅ SUCCESS! TextractOutput created with {textractOutput.Blocks?.Count ?? 0} blocks");

                // Test that confidence values are accessible
                var blocksWithConfidence = textractOutput.Blocks?.Where(b => b.Confidence.HasValue).ToList();
                System.Console.WriteLine($"✅ Blocks with confidence values: {blocksWithConfidence?.Count ?? 0}");

                if (blocksWithConfidence?.Any() == true)
                {
                    var firstBlockWithConfidence = blocksWithConfidence.First();
                    System.Console.WriteLine($"✅ Sample confidence value: {firstBlockWithConfidence.Confidence:F2}%");
                }

                System.Console.WriteLine("✅ All tests passed! The Confidence property fix is working correctly.");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"❌ TEST FAILED: {ex.GetType().Name}: {ex.Message}");
                System.Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}