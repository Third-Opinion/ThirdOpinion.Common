# ThirdOpinion.Common.Fhir.Documents

## Purpose

This library provides services for downloading and organizing FHIR DocumentReference content from AWS HealthLake to S3
storage, with comprehensive metadata extraction and file organization capabilities.

## Core Components

### Main Services

- **HealthLakeDocumentDownloadService** - Orchestrates the entire document download process from HealthLake to S3
- **PatientEverythingService** - Retrieves all FHIR resources for a patient using the `$everything` operation
- **BinaryDownloadService** - Downloads Binary resources referenced by DocumentReferences
- **BundleParserService** - Parses FHIR Bundle responses and extracts DocumentReferences

### File Management

- **FileOrganizationService** - Generates S3 keys and organizes files by practice/patient structure
- **FileNamingService** - Creates consistent file names with proper extensions
- **Base64ContentExtractor** - Extracts and decodes embedded content from FHIR attachments
- **MetadataExtractorService** - Extracts FHIR metadata for S3 tags

### Storage & Organization

- **S3StorageService** - Handles S3 upload operations with metadata tagging
- **PracticeInfo** - Models practice information for file organization
- **S3TagSet** - Manages S3 metadata tags extracted from FHIR resources

## Usage Patterns

### Basic Document Download

```csharp
// Download all documents for a patient
var results = await documentDownloadService.DownloadPatientDocumentReferencesAsync(
    patientId: "patient-123",
    overridePracticeId: "practice-456", // Optional
    s3KeyPrefix: "documents/", // Optional
    s3Bucket: "my-documents-bucket", // Optional
    force: true // Skip confirmation prompts
);

// Check results
foreach (var result in results)
{
    Console.WriteLine($"DocumentReference {result.DocumentReferenceId}: " +
                     $"{result.SuccessfulFiles}/{result.TotalFiles} files downloaded");
}
```

### Single DocumentReference Download

```csharp
// Download a specific DocumentReference
var result = await documentDownloadService.DownloadDocumentReferenceAsync(
    documentReferenceId: "docref-789",
    overridePatientId: "patient-123", // Optional if using direct retrieval
    s3Bucket: "my-documents-bucket"
);

Console.WriteLine($"Downloaded {result.TotalBytes} bytes in {result.Duration}");
```

### File Organization Structure

```
s3://bucket/
├── PracticeName_PracticeId/
│   ├── patient-123/
│   │   ├── docref-789_0_document.pdf
│   │   ├── docref-789_1_image.jpg
│   │   └── binary-456_attachment.xml
│   └── patient-456/
│       └── docref-101_0_report.pdf
└── UnknownPractice/
    └── patient-789/
        └── docref-202_0_summary.txt
```

## Key Features

### Content Types Supported

- **Embedded Content**: Base64-encoded data within DocumentReference attachments
- **Binary References**: Links to separate Binary FHIR resources
- **Multiple Attachments**: DocumentReferences with multiple content attachments

### Metadata Extraction

Automatically extracts and stores as S3 tags:

- Document categories (US Core DocumentReference categories)
- Encounter references
- FHIR meta information (lastUpdated, versionId)
- Document status
- Practice and patient information

### Error Handling & Resilience

- Detailed error reporting for failed downloads
- Graceful handling of missing or invalid resources
- Progress tracking for large batch operations
- Comprehensive logging with correlation IDs

### Validation & Filtering

- Skips documents with "entered-in-error" status
- Validates FHIR Bundle responses
- Handles missing or empty content gracefully
- User confirmation prompts (unless forced)

## Configuration Requirements

### Dependencies

```csharp
services.AddHealthLakeServices(config =>
{
    config.Region = "us-east-2";
    config.DatastoreId = "your-datastore-id";
});

services.AddAmazonS3();
services.AddLogging();
```

### Required Services

- AWS HealthLake client configuration
- AWS S3 client configuration
- HealthLake HTTP service for signed requests
- Correlation ID provider for request tracking
- Standard .NET logging infrastructure

## Best Practices

### Batch Operations

- Use `DownloadPatientDocumentReferencesAsync` for comprehensive patient document retrieval
- Monitor progress through logging output
- Handle partial failures gracefully

### S3 Organization

- Use consistent practice ID overrides for better organization
- Implement proper S3 bucket policies for access control
- Consider S3 lifecycle policies for cost optimization

### Error Management

- Check `DocumentDownloadResults.Success` for overall operation status
- Review `FileResults` for individual file-level errors
- Use correlation IDs to track operations across logs

### Performance Considerations

- Parallel processing is built-in for multiple DocumentReferences
- Large Binary resources are streamed directly to S3
- Use appropriate S3 storage classes (configured as StandardInfrequentAccess by default)

## Common Patterns

### Retry Logic

The service includes built-in retry mechanisms, but for additional resilience:

```csharp
// Retry failed downloads
var failedResults = results.Where(r => !r.Success);
foreach (var failed in failedResults)
{
    // Implement custom retry logic if needed
    await documentDownloadService.DownloadDocumentReferenceAsync(failed.DocumentReferenceId);
}
```

### Progress Monitoring

```csharp
// Enable detailed logging to monitor progress
services.AddLogging(builder =>
{
    builder.SetMinimumLevel(LogLevel.Information);
    builder.AddConsole();
});
```

### Custom File Organization

```csharp
// Override practice information for consistent organization
var results = await documentDownloadService.DownloadPatientDocumentReferencesAsync(
    patientId: "patient-123",
    overridePracticeId: "standardized-practice-id",
    s3KeyPrefix: "clinical-documents/"
);
```