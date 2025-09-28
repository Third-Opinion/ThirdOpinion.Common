# ThirdOpinion.Common.Textract - NuGet Package Guide

## Overview

ThirdOpinion.Common.Textract is a .NET library that provides a simplified interface for AWS Textract OCR services. It handles document text extraction, response processing, and conversion to various formats optimized for different use cases.

## Installation

```bash
# Install via Package Manager
Install-Package ThirdOpinion.Common.Textract

# Or via .NET CLI
dotnet add package ThirdOpinion.Common.Textract

# Or add to .csproj
<PackageReference Include="ThirdOpinion.Common.Textract" Version="1.0.0" />
```

## Prerequisites

1. **AWS Credentials**: Configure AWS credentials using one of these methods:
   - AWS CLI: `aws configure`
   - Environment variables: `AWS_ACCESS_KEY_ID` and `AWS_SECRET_ACCESS_KEY`
   - IAM role (for EC2/Lambda)
   - AWS profile: Set `AWS_PROFILE` environment variable

2. **AWS Permissions**: Ensure your AWS credentials have the following Textract permissions:
   - `textract:DetectDocumentText`
   - `textract:StartDocumentTextDetection`
   - `textract:GetDocumentTextDetection`
   - `s3:GetObject` (if processing S3 documents)

## Quick Start

### Basic Usage - Detect Text in Image

```csharp
using Amazon.Textract;
using ThirdOpinion.Common.Textract.Services;
using ThirdOpinion.Common.Textract.Models;

// Initialize AWS Textract client
var region = Amazon.RegionEndpoint.USEast2;
var textractClient = new AmazonTextractClient(region);

// Create service instance
var textractService = new TextractTextDetectionService(textractClient);

// Process a local image file
byte[] imageBytes = File.ReadAllBytes("document.png");
var response = await textractService.DetectDocumentTextAsync(imageBytes);

// Convert to TextractOutput for easier processing
var textractOutput = TextractOutput.FromAmazonTextractResponse(response);

// Extract plain text
string documentText = textractOutput.ToPlainText();
Console.WriteLine(documentText);
```

### Process PDF from S3

```csharp
using Amazon.Textract.Model;

// For multi-page PDFs in S3, use async document processing
var s3Object = new S3Object
{
    Bucket = "my-bucket",
    Name = "document.pdf"
};

// Start async job
var startRequest = new StartDocumentTextDetectionRequest
{
    DocumentLocation = new DocumentLocation { S3Object = s3Object }
};

var startResponse = await textractClient.StartDocumentTextDetectionAsync(startRequest);
string jobId = startResponse.JobId;

// Poll for completion (in production, use SNS notifications)
GetDocumentTextDetectionResponse result = null;
while (true)
{
    var getRequest = new GetDocumentTextDetectionRequest { JobId = jobId };
    result = await textractClient.GetDocumentTextDetectionAsync(getRequest);

    if (result.JobStatus == JobStatus.SUCCEEDED)
        break;
    else if (result.JobStatus == JobStatus.FAILED)
        throw new Exception($"Textract job failed: {result.StatusMessage}");

    await Task.Delay(5000); // Wait 5 seconds before polling again
}

// Convert to TextractOutput
var textractOutput = TextractOutput.FromAmazonTextractResponse(result);
```

## Key Features

### 1. TextractOutput Model

The `TextractOutput` class provides a simplified structure for working with Textract responses:

```csharp
// Access document metadata
int pageCount = textractOutput.Pages;
var metadata = textractOutput.DocumentMetadata;

// Access blocks (text elements)
foreach (var block in textractOutput.Blocks)
{
    if (block.BlockType == BlockTypeEnum.LINE)
    {
        Console.WriteLine($"Line: {block.Text}");
        Console.WriteLine($"Confidence: {block.Confidence}%");
    }
}
```

### 2. Text Extraction Methods

```csharp
// Plain text extraction
string plainText = textractOutput.ToPlainText();

// Knowledge base formatted JSON (optimized for RAG/LLM systems)
string kbJson = TextractOutputExtensions.ToKBJsonString(
    new List<TextractOutput> { textractOutput }
);

// Filtered JSON (excludes geometry data for smaller payload)
string filteredJson = TextractOutputExtensions.ToFilteredJsonString(
    new List<TextractOutput> { textractOutput }
);
```

### 3. Merge Multiple Documents

```csharp
// Merge multiple single-page scans into one document
var page1Output = TextractOutput.FromAmazonTextractResponse(page1Response);
var page2Output = TextractOutput.FromAmazonTextractResponse(page2Response);
var page3Output = TextractOutput.FromAmazonTextractResponse(page3Response);

// Merge and renumber pages
string mergedJson = TextractOutputExtensions.ToFilteredJsonString(
    new List<TextractOutput> { page1Output, page2Output, page3Output }
);
```

### 4. Performance Optimization

```csharp
// Skip geometry data to reduce memory usage
var textractOutput = TextractOutput.FromAmazonTextractResponse(
    response,
    noGeo: true,        // Skip polygon data
    noRelationships: false
);

// Skip relationships for faster processing
var lightOutput = TextractOutput.FromAmazonTextractResponse(
    response,
    noGeo: true,
    noRelationships: true  // Skip block relationships
);
```

## Advanced Usage

### Custom Text Processing with Metadata

```csharp
// Extract text with metadata for auditing
var blocks = textractOutput.Blocks
    .Where(b => b.BlockType == BlockTypeEnum.LINE)
    .Select(b => new
    {
        Text = b.Text,
        Confidence = b.Confidence,
        Page = b.Page,
        BlockId = b.Id,
        Position = new
        {
            Top = b.Geometry?.BoundingBox?.Top,
            Left = b.Geometry?.BoundingBox?.Left
        }
    });

foreach (var block in blocks)
{
    Console.WriteLine($"[Page {block.Page}] {block.Text} (Confidence: {block.Confidence:F1}%)");
}
```

### Table Extraction

```csharp
// Extract tables from document
var tableBlocks = textractOutput.Blocks
    .Where(b => b.BlockType == BlockTypeEnum.TABLE)
    .ToList();

foreach (var table in tableBlocks)
{
    // Get cells for this table
    var cellIds = table.Relationships?
        .Where(r => r.Type == "CHILD")
        .SelectMany(r => r.Ids)
        .ToList() ?? new List<string>();

    var cells = textractOutput.Blocks
        .Where(b => b.BlockType == BlockTypeEnum.CELL && cellIds.Contains(b.Id))
        .OrderBy(c => c.RowIndex)
        .ThenBy(c => c.ColumnIndex);

    // Process table cells...
}
```

### Confidence Filtering

```csharp
// Filter out low-confidence text
const float MIN_CONFIDENCE = 90.0f;

var highConfidenceText = textractOutput.Blocks
    .Where(b => b.BlockType == BlockTypeEnum.LINE &&
                b.Confidence.HasValue &&
                b.Confidence.Value >= MIN_CONFIDENCE)
    .Select(b => b.Text)
    .ToList();
```

## Error Handling

```csharp
try
{
    var response = await textractService.DetectDocumentTextAsync(imageBytes);
    var output = TextractOutput.FromAmazonTextractResponse(response);
    // Process output...
}
catch (AmazonTextractException ex)
{
    // Handle Textract-specific errors
    Console.WriteLine($"Textract error: {ex.ErrorCode} - {ex.Message}");

    switch (ex.ErrorCode)
    {
        case "ProvisionedThroughputExceededException":
            // Implement retry with backoff
            break;
        case "InvalidS3ObjectException":
            // Handle invalid S3 object
            break;
        case "UnsupportedDocumentException":
            // Document format not supported
            break;
    }
}
catch (Exception ex)
{
    // Handle general errors
    Console.WriteLine($"Error processing document: {ex.Message}");
}
```

## Best Practices

### 1. Memory Management

```csharp
// For large documents, process in chunks and dispose
using (var jsonDoc = JsonDocument.Parse(filteredJson))
{
    // Process JSON document
    var root = jsonDoc.RootElement;
    // ...
}

// Clear large collections when done
textractOutput.Blocks?.Clear();
textractOutput = null;
GC.Collect(); // Force collection for very large documents
```

### 2. Cost Optimization

```csharp
// Use DetectDocumentText for simple text extraction (cheaper)
// Only use AnalyzeDocument when you need forms/tables

// Cache results to avoid reprocessing
var cacheKey = $"textract_{documentHash}";
if (cache.TryGetValue(cacheKey, out var cachedResult))
{
    return cachedResult;
}
```

### 3. Async Processing for Large Documents

```csharp
// Use async processing for PDFs and large documents
public async Task<TextractOutput> ProcessLargeDocumentAsync(
    string s3Bucket,
    string s3Key,
    CancellationToken cancellationToken = default)
{
    // Start job
    var startRequest = new StartDocumentTextDetectionRequest
    {
        DocumentLocation = new DocumentLocation
        {
            S3Object = new S3Object { Bucket = s3Bucket, Name = s3Key }
        },
        NotificationChannel = new NotificationChannel
        {
            SNSTopicArn = "arn:aws:sns:us-east-2:123456789:textract-notifications",
            RoleArn = "arn:aws:iam::123456789:role/TextractRole"
        }
    };

    var startResponse = await textractClient.StartDocumentTextDetectionAsync(
        startRequest,
        cancellationToken);

    // Wait for SNS notification or poll
    // ... implementation

    return textractOutput;
}
```

## Testing

```csharp
// Unit test example
[Fact]
public void TestTextractOutputConversion()
{
    // Arrange - Create mock response
    var response = new DetectDocumentTextResponse
    {
        DetectDocumentTextModelVersion = "1.0",
        Blocks = new List<Block>
        {
            new Block
            {
                BlockType = "LINE",
                Text = "Test text",
                Confidence = 99.5f
            }
        }
    };

    // Act
    var output = TextractOutput.FromAmazonTextractResponse(response);

    // Assert
    Assert.NotNull(output);
    Assert.Single(output.Blocks);
    Assert.Equal("Test text", output.Blocks[0].Text);
}
```

## Configuration

### appsettings.json

```json
{
  "AWS": {
    "Region": "us-east-2",
    "Textract": {
      "MaxRetries": 3,
      "TimeoutSeconds": 30,
      "EnableLogging": true
    }
  }
}
```

### Dependency Injection

```csharp
// In Startup.cs or Program.cs
services.AddSingleton<IAmazonTextract>(provider =>
{
    var config = provider.GetRequiredService<IConfiguration>();
    var region = RegionEndpoint.GetBySystemName(config["AWS:Region"]);
    return new AmazonTextractClient(region);
});

services.AddScoped<ITextractTextDetectionService, TextractTextDetectionService>();
```

## Troubleshooting

### Common Issues

1. **Credentials not found**
   ```bash
   export AWS_PROFILE=your-profile
   # Or
   export AWS_ACCESS_KEY_ID=your-key
   export AWS_SECRET_ACCESS_KEY=your-secret
   ```

2. **Region endpoint errors**
   ```csharp
   // Explicitly set region
   var config = new AmazonTextractConfig
   {
       RegionEndpoint = RegionEndpoint.USEast2
   };
   var client = new AmazonTextractClient(config);
   ```

3. **Memory issues with large documents**
   ```csharp
   // Use streaming for large results
   var output = TextractOutput.FromAmazonTextractResponse(
       response,
       noGeo: true,  // Skip geometry to save memory
       noRelationships: true
   );
   ```

## Performance Tips

1. **Batch Processing**: Process multiple documents in parallel
2. **Caching**: Cache Textract results to avoid reprocessing
3. **Compression**: Compress images before sending to Textract
4. **Format Optimization**: Use PNG for text, JPEG for photos
5. **Resolution**: 150-300 DPI is optimal for most documents

## License and Support

This package is part of ThirdOpinion.Common libraries. For issues, feature requests, or contributions, please contact the development team.

## Version History

- **1.0.0**: Initial release with basic text detection support
- **1.1.0**: Added LINQ-based processing for better performance
- **1.2.0**: Added support for table extraction and forms

---

*Last updated: 2024*