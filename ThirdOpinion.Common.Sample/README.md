# ThirdOpinion.Common.Sample

A console application for smoke testing AWS HealthLake integration. This application retrieves a FHIR DocumentReference
resource, extracts and decodes base64-encoded document content, and saves it to a file.

## Features

- ✅ Retrieves DocumentReference resources from AWS HealthLake
- ✅ Extracts base64-encoded document content from FHIR attachments
- ✅ Decodes and saves documents to files with appropriate extensions
- ✅ Supports multiple document types (PDF, images, text, etc.)
- ✅ Comprehensive logging and error handling
- ✅ AWS authentication using standard credential chains

## Prerequisites

1. **AWS Credentials**: Configure AWS credentials using one of these methods:
    - AWS CLI: `aws configure`
    - Environment variables: `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`
    - IAM roles (when running on EC2/ECS)
    - AWS SSO profiles

2. **HealthLake Access**: Ensure you have permission to read from the HealthLake datastore:
    - `healthlake:ReadResource` permission
    - Access to the specific HealthLake datastore

3. **DocumentReference ID**: A valid DocumentReference resource ID that exists in your HealthLake datastore

## Configuration

Edit `appsettings.json` to configure the application:

```json
{
  "HealthLake": {
    "DatastoreId": "your-datastore-id-here",
    "DatastoreEndpoint": "healthlake.us-east-2.amazonaws.com",
    "Region": "us-east-2"
  },
  "Sample": {
    "DocumentReferenceId": "optional-default-document-id",
    "OutputDirectory": "./output"
  }
}
```

## Usage

### Command Line

```bash
# Run with DocumentReference ID as argument
dotnet run -- "document-reference-123"

# Run with ID from configuration
dotnet run
```

### Build and Run

```bash
# Build the application
dotnet build

# Run with specific DocumentReference ID
dotnet run --project ThirdOpinion.Common.Sample -- "your-document-reference-id"
```

## Example Output

```
[INFO] Starting ThirdOpinion.Common.Sample smoke test
[INFO] Using DocumentReference ID from command line: document-123
[INFO] Testing HealthLake connectivity...
[INFO] Retrieving DocumentReference document-123...
[INFO] Successfully extracted document content from DocumentReference document-123. MIME Type: application/pdf, Filename: medical-report.pdf, Base64 Length: 45678
[INFO] Saving decoded document to ./output/medical-report.pdf (33456 bytes)
[INFO] Successfully completed smoke test!
✓ DocumentReference document-123 retrieved successfully
✓ Document content decoded (33456 bytes)
✓ Document saved to: ./output/medical-report.pdf
✓ MIME Type: application/pdf
```

## Supported Document Types

The application automatically detects file extensions based on MIME type:

- **PDF**: `application/pdf` → `.pdf`
- **Images**: `image/jpeg` → `.jpg`, `image/png` → `.png`
- **Documents**: `application/msword` → `.doc`,
  `application/vnd.openxmlformats-officedocument.wordprocessingml.document` → `.docx`
- **Text**: `text/plain` → `.txt`
- **Binary**: Unknown types → `.bin`

## Error Handling

The application provides detailed error messages for common issues:

- **Missing DocumentReference ID**: Returns exit code 1
- **HealthLake connectivity issues**: Logs AWS authentication/network errors
- **DocumentReference not found**: Logs HTTP 404 errors
- **Invalid base64 content**: Logs decoding errors
- **File system errors**: Logs issues writing output files

## FHIR DocumentReference Structure

The application expects DocumentReference resources with this structure:

```json
{
  "resourceType": "DocumentReference",
  "id": "document-123",
  "content": [
    {
      "attachment": {
        "contentType": "application/pdf",
        "title": "medical-report.pdf",
        "data": "base64-encoded-content-here..."
      }
    }
  ]
}
```

## Troubleshooting

### AWS Authentication Issues

- Verify AWS credentials: `aws sts get-caller-identity`
- Check IAM permissions for HealthLake access
- Ensure the correct AWS region is configured

### HealthLake Issues

- Verify the datastore ID exists and is active
- Check the datastore endpoint URL
- Ensure the DocumentReference ID exists in the datastore

### Document Extraction Issues

- Verify the DocumentReference has a `content` array with `attachment.data`
- Check that the base64 content is valid
- Ensure sufficient disk space for output files

## Development

The application uses these key components:

- **HealthLakeReaderService**: Handles FHIR resource retrieval and content extraction
- **Program.cs**: Main application logic and dependency injection setup
- **DocumentContent**: Data model for extracted document content

To extend the application:

1. Modify `HealthLakeReaderService` to support additional FHIR resource types
2. Add new MIME type mappings in `DocumentContent.GetFileExtension()`
3. Extend configuration options in `appsettings.json`