using Amazon.Textract;
using ThirdOpinion.Common.Textract.Services;
using ThirdOpinion.Common.Textract.Models;
using System.Text.Json;
using Amazon.Runtime.CredentialManagement;

namespace TextractLib.Console;

class Program
{
    static async Task Main(string[] args)
    {
        // Check if test mode is requested
        if (args.Length > 0 && args[0] == "--test")
        {
            TestConfidenceFix.RunTest();
            return;
        }

        // Ensure AWS_PROFILE is set in environment or use default profile`
        var awsProfile = Environment.GetEnvironmentVariable("AWS_PROFILE");
        if (string.IsNullOrEmpty(awsProfile))
        {
            System.Console.WriteLine("WARNING: AWS_PROFILE environment variable is not set. Default profile will be used.");
        }
        else
        {
            System.Console.WriteLine($"Using AWS profile: {awsProfile}");
        }
        // Get region from AWS configuration or use default
        var regionName = Environment.GetEnvironmentVariable("AWS_REGION") ??
                        Environment.GetEnvironmentVariable("AWS_DEFAULT_REGION") ??
                        "us-east-2";
        var region = Amazon.RegionEndpoint.GetBySystemName(regionName);

        // Create AWS client - will automatically use AWS_PROFILE environment variable
        var textractClient = new Amazon.Textract.AmazonTextractClient(region);
        var textractService = new TextractTextDetectionService(textractClient);

        System.Console.WriteLine("TextractLib Console Demo");
        System.Console.WriteLine("========================");

        // Debug AWS configuration from environment (avoiding SDK configuration extensions)
        // System.Console.WriteLine("DEBUG: AWS Configuration Details:");
        // var envRegion = Environment.GetEnvironmentVariable("AWS_REGION") ?? Environment.GetEnvironmentVariable("AWS_DEFAULT_REGION");
        // var envProfile = Environment.GetEnvironmentVariable("AWS_PROFILE");
        // var accessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
        // var secretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");
        //
        // System.Console.WriteLine($"DEBUG: AWS_REGION env var: {envRegion ?? "Not set"}");
        // System.Console.WriteLine($"DEBUG: AWS_PROFILE env var: {envProfile ?? "Not set"}");
        // System.Console.WriteLine($"DEBUG: AWS_ACCESS_KEY_ID: {(string.IsNullOrEmpty(accessKey) ? "Not set" : "Set")}");
        // System.Console.WriteLine($"DEBUG: AWS_SECRET_ACCESS_KEY: {(string.IsNullOrEmpty(secretKey) ? "Not set" : "Set")}");
        // System.Console.WriteLine();

        const string pdfFilePath = "rad_report_1.pdf";

        if (!File.Exists(pdfFilePath))
        {
            System.Console.WriteLine($"Error: PDF file '{pdfFilePath}' not found in current directory.");
            System.Console.WriteLine("Please place the rad_report_1.pdf file in the same directory as this executable.");
            return;
        }

        try
        {
            System.Console.WriteLine($"Processing PDF file: {pdfFilePath}");
            System.Console.WriteLine($"File size: {new FileInfo(pdfFilePath).Length} bytes");
            System.Console.WriteLine("Calling AWS Textract...");

            // Debug: Check AWS configuration
            System.Console.WriteLine("DEBUG: Checking AWS configuration...");

            // Use Textract to extract text from the local PDF file
            var response = await textractService.DetectTextLocalAsync(pdfFilePath);

            System.Console.WriteLine("DEBUG: Textract response received.");
            System.Console.WriteLine($"DEBUG: Response blocks count: {response.Blocks?.Count ?? 0}");
            System.Console.WriteLine($"DEBUG: Document metadata: {response.DocumentMetadata?.Pages ?? 0} pages");

            if (response.Blocks != null)
            {
                var lineBlocks = response.Blocks.Where(b => b.BlockType == "LINE").ToList();
                var wordBlocks = response.Blocks.Where(b => b.BlockType == "WORD").ToList();
                System.Console.WriteLine($"DEBUG: Line blocks: {lineBlocks.Count}, Word blocks: {wordBlocks.Count}");

                // Show first few lines for debugging
                var firstLines = lineBlocks.Take(3).Select(b => b.Text).ToList();
                System.Console.WriteLine($"DEBUG: First few lines: {string.Join(", ", firstLines)}");
            }

            System.Console.WriteLine("Textract processing completed. Converting to TextractOutput format...");

            // Convert to TextractOutput format
            System.Console.WriteLine("DEBUG: Creating TextractOutput from response...");
            var textractOutput = TextractOutputExtensions.TextractOutput.FromAmazonTextractResponse(response);
            var textractOutputList = new List<TextractOutputExtensions.TextractOutput> { textractOutput };

            System.Console.WriteLine($"DEBUG: TextractOutput created. Blocks count: {textractOutput.Blocks?.Count ?? 0}");

            // Use ToKBJsonString to get structured content, then convert to markdown
            System.Console.WriteLine("DEBUG: Converting to KB JSON string...");
            var kbJsonString = TextractOutputExtensions.ToKBJsonString(textractOutputList);

            System.Console.WriteLine($"DEBUG: KB JSON string length: {kbJsonString?.Length ?? 0}");
            if (!string.IsNullOrEmpty(kbJsonString))
            {
                System.Console.WriteLine($"DEBUG: KB JSON preview (first 200 chars): {kbJsonString[..Math.Min(200, kbJsonString.Length)]}...");
            }
            else
            {
                System.Console.WriteLine("DEBUG: WARNING - KB JSON string is null or empty!");
            }

            // Convert to simple markdown format
            System.Console.WriteLine("DEBUG: Converting KB JSON to markdown...");
            var markdownContent = ConvertKBJsonToMarkdown(kbJsonString, pdfFilePath);

            System.Console.WriteLine($"DEBUG: Markdown content length: {markdownContent.Length}");

            // Save to markdown file
            var outputFileName = Path.ChangeExtension(pdfFilePath, ".md");
            await File.WriteAllTextAsync(outputFileName, markdownContent);

            System.Console.WriteLine($"Markdown conversion completed! Output saved to: {outputFileName}");
            System.Console.WriteLine();
            System.Console.WriteLine("Preview of extracted content:");
            System.Console.WriteLine("=============================");
            System.Console.WriteLine(markdownContent.Length > 1000
                ? markdownContent[..1000] + "..."
                : markdownContent);
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"ERROR: {ex.GetType().Name}: {ex.Message}");
            System.Console.WriteLine($"DEBUG: Stack trace: {ex.StackTrace}");

            if (ex.InnerException != null)
            {
                System.Console.WriteLine($"DEBUG: Inner exception: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
            }

            // Check AWS credentials and configuration
            System.Console.WriteLine();
            System.Console.WriteLine("DEBUG: AWS Configuration Check:");
            try
            {
                var awsRegion = Environment.GetEnvironmentVariable("AWS_REGION") ?? Environment.GetEnvironmentVariable("AWS_DEFAULT_REGION");
                var debugAwsProfile = Environment.GetEnvironmentVariable("AWS_PROFILE");
                var awsAccessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
                var awsSecretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");

                System.Console.WriteLine($"DEBUG: AWS_REGION: {awsRegion ?? "Not set"}");
                System.Console.WriteLine($"DEBUG: AWS_PROFILE: {debugAwsProfile ?? "Not set"}");
                System.Console.WriteLine($"DEBUG: AWS_ACCESS_KEY_ID: {(string.IsNullOrEmpty(awsAccessKey) ? "Not set" : "Set")}");
                System.Console.WriteLine($"DEBUG: AWS_SECRET_ACCESS_KEY: {(string.IsNullOrEmpty(awsSecretKey) ? "Not set" : "Set")}");

                // Check appsettings.json
                var appSettingsPath = "appsettings.json";
                if (File.Exists(appSettingsPath))
                {
                    var appSettings = await File.ReadAllTextAsync(appSettingsPath);
                    System.Console.WriteLine($"DEBUG: appsettings.json exists and contains: {appSettings.Length} characters");
                }
                else
                {
                    System.Console.WriteLine("DEBUG: appsettings.json not found");
                }
            }
            catch (Exception debugEx)
            {
                System.Console.WriteLine($"DEBUG: Error checking AWS configuration: {debugEx.Message}");
            }

            System.Console.WriteLine();
            System.Console.WriteLine("TROUBLESHOOTING TIPS:");
            System.Console.WriteLine("1. Make sure your AWS credentials are configured (AWS CLI: 'aws configure')");
            System.Console.WriteLine("2. Ensure you have Amazon Textract permissions");
            System.Console.WriteLine("3. Check that the PDF file is valid and not corrupted");
            System.Console.WriteLine("4. Verify your AWS region supports Textract");
            System.Console.WriteLine("5. Check appsettings.json for AWS configuration");
        }
    }

    private static string ConvertKBJsonToMarkdown(string? kbJsonString, string fileName)
    {
        var markdown = new System.Text.StringBuilder();

        // Add header
        markdown.AppendLine("# Extracted Document Content");
        markdown.AppendLine();
        markdown.AppendLine($"**Source:** {fileName}");
        markdown.AppendLine($"**Processed on:** {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        markdown.AppendLine();

        if (string.IsNullOrWhiteSpace(kbJsonString))
        {
            markdown.AppendLine("*No content extracted from the document.*");
            return markdown.ToString();
        }

        try
        {
            // Parse the KB JSON to extract structured content
            using var document = JsonDocument.Parse(kbJsonString);
            var root = document.RootElement;

            // Check if there are blocks in the KB JSON
            if (root.TryGetProperty("blocks", out var blocksElement) && blocksElement.GetArrayLength() > 0)
            {
                foreach (var block in blocksElement.EnumerateArray())
                {
                    if (block.TryGetProperty("text", out var textProperty))
                    {
                        var text = textProperty.GetString()?.Trim();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            // Apply simple formatting based on text characteristics
                            if (IsLikelyTitle(text))
                            {
                                markdown.AppendLine($"## {text}");
                            }
                            else
                            {
                                markdown.AppendLine(text);
                            }
                            markdown.AppendLine();
                        }
                    }
                }
            }
            else
            {
                // Fallback: display the raw KB JSON content as a code block
                markdown.AppendLine("## Raw Knowledge Base Content");
                markdown.AppendLine();
                markdown.AppendLine("```json");
                markdown.AppendLine(kbJsonString);
                markdown.AppendLine("```");
            }
        }
        catch (JsonException)
        {
            // If JSON parsing fails, treat as plain text
            markdown.AppendLine("## Document Text");
            markdown.AppendLine();
            markdown.AppendLine(kbJsonString);
        }

        return markdown.ToString();
    }

    private static bool IsLikelyTitle(string text)
    {
        // Simple heuristic for detecting titles
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length <= 8 && words.All(word =>
            char.IsUpper(word[0]) ||
            char.IsDigit(word[0]) ||
            word.Length <= 3); // Short words like "of", "the", etc.
    }

}