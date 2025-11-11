# Document Reference Builders

The Document Reference builders create FHIR DocumentReference resources for various document processing workflows
including OCR text extraction and clinical fact extraction.

## Overview

Document builders handle the creation of FHIR R4 DocumentReference resources that represent clinical documents and their
processed outputs. These builders support:

- **OCR text extraction** from scanned documents
- **Clinical fact extraction** from processed text
- **Multi-format content support** (PDF, images, text)
- **AWS S3 integration** for large document storage
- **Processing metadata** and AI confidence tracking

## Available Builders

### OcrDocumentReferenceBuilder

Creates DocumentReference resources representing OCR text extraction results from scanned clinical documents.

### FactExtractionDocumentReferenceBuilder

Creates DocumentReference resources representing clinical fact extraction results from processed text.

## Required Dependencies

```csharp
using Hl7.Fhir.Model;
using ThirdOpinion.Common.Fhir.Builders.Documents;
using ThirdOpinion.Common.Fhir.Configuration;
using ThirdOpinion.Common.Fhir.Helpers;
```

## OcrDocumentReferenceBuilder

### Purpose

The `OcrDocumentReferenceBuilder` creates FHIR DocumentReference resources for OCR (Optical Character Recognition)
processed documents. It captures both the source document information and the extracted text content.

### Basic Usage Example

```csharp
// Create configuration
var config = AiInferenceConfiguration.CreateDefault();

// Build OCR Document Reference
var ocrDocument = new OcrDocumentReferenceBuilder(config)
    .WithFhirResourceId("ocr-process-001")
    .WithPatient("Patient/patient-123", "John Smith")
    .WithDevice("Device/ocr-engine-v2", "Advanced OCR Engine v2.1")
    .WithSourceDocument("DocumentReference/scan-001", "Original Pathology Report Scan")
    .WithContentText("PATHOLOGY REPORT\n\nPatient: John Smith\nDOB: 1975-03-15\n\n" +
                     "DIAGNOSIS: Adenocarcinoma of prostate\nGleason Score: 4+3=7\n" +
                     "Tumor Stage: pT3a\nMargins: Positive at apex\n\n" +
                     "MICROSCOPIC DESCRIPTION:\nSections show prostatic adenocarcinoma...")
    .WithContentUrl("https://bucket.s3.amazonaws.com/docs/patient-123/ocr-text-001.txt")
    .WithSize(15_248) // 15KB text file
    .WithProcessingDate(DateTime.UtcNow)
    .WithConfidence(0.94f)
    .AddProcessingNote("High quality scan with clear text")
    .AddProcessingNote("Some handwritten annotations not captured")
    .Build();
```

### API Reference

#### Required Methods

These methods **must** be called before `Build()`:

- `WithPatient(ResourceReference)` - Patient reference
- `WithDevice(ResourceReference)` - OCR device/system performing extraction
- `WithSourceDocument(ResourceReference)` - Reference to original scanned document
- `WithContentText(string)` - Extracted text content

#### Optional Methods

- `WithFhirResourceId(string)` - Custom FHIR resource ID (auto-generated if not provided)
- `WithContentUrl(string)` - URL to stored text file (S3, etc.)
- `WithSize(long)` - Size of extracted content in bytes
- `WithProcessingDate(DateTime)` - When OCR processing occurred
- `WithConfidence(float)` - OCR accuracy confidence (0.0-1.0)
- `AddProcessingNote(string)` - Add notes about processing quality or issues

### Advanced Example with S3 Storage

```csharp
var ocrDocument = new OcrDocumentReferenceBuilder(config)
    .WithPatient("Patient/patient-456")
    .WithDevice("Device/aws-textract", "Amazon Textract OCR Service")
    .WithSourceDocument("DocumentReference/radiology-scan-456")
    .WithContentText(extractedText)
    .WithContentUrl("https://clinical-docs.s3.amazonaws.com/ocr/patient-456/radiology-ocr.txt")
    .WithSize(ocrTextBytes.Length)
    .WithProcessingDate(DateTime.UtcNow)
    .WithConfidence(0.97f)
    .AddProcessingNote("Processed with AWS Textract standard model")
    .AddProcessingNote("Manual review recommended for handwritten sections")
    .Build();
```

## FactExtractionDocumentReferenceBuilder

### Purpose

The `FactExtractionDocumentReferenceBuilder` creates FHIR DocumentReference resources for clinical fact extraction
results. It represents the structured clinical facts extracted from processed text using AI/NLP systems.

### Basic Usage Example

```csharp
// Clinical facts extracted from text
var extractedFacts = new[]
{
    new Fact
    {
        factGuid = "fact-001",
        factDocumentReference = "DocumentReference/ocr-text-001",
        type = "diagnosis",
        fact = "Adenocarcinoma of prostate",
        @ref = new[] { "Line 5", "Diagnosis section" },
        timeRef = "2024-01-15",
        relevance = "Primary diagnosis for treatment planning"
    },
    new Fact
    {
        factGuid = "fact-002",
        factDocumentReference = "DocumentReference/ocr-text-001",
        type = "assessment",
        fact = "Gleason Score 4+3=7",
        @ref = new[] { "Line 6" },
        timeRef = "2024-01-15",
        relevance = "Prognostic indicator for risk stratification"
    }
};

// Build Fact Extraction Document Reference
var factDocument = new FactExtractionDocumentReferenceBuilder(config)
    .WithFhirResourceId("fact-extraction-001")
    .WithPatient("Patient/patient-123", "John Smith")
    .WithDevice("Device/fact-extractor-ai", "Clinical Fact Extractor AI v3.0")
    .WithSourceDocument("DocumentReference/ocr-text-001", "OCR Extracted Text")
    .WithExtractedFacts(extractedFacts)
    .WithFactsUrl("https://bucket.s3.amazonaws.com/facts/patient-123/facts-001.json")
    .WithSize(2_048) // 2KB JSON file
    .WithProcessingDate(DateTime.UtcNow)
    .WithConfidence(0.89f)
    .AddProcessingNote("Extracted 15 clinical facts")
    .AddProcessingNote("High confidence in diagnosis and staging facts")
    .Build();
```

### API Reference

#### Required Methods

These methods **must** be called before `Build()`:

- `WithPatient(ResourceReference)` - Patient reference
- `WithDevice(ResourceReference)` - AI system performing fact extraction
- `WithSourceDocument(ResourceReference)` - Reference to source document (usually OCR text)
- `WithExtractedFacts(Fact[])` - Array of extracted clinical facts

#### Optional Methods

- `WithFhirResourceId(string)` - Custom FHIR resource ID (auto-generated if not provided)
- `WithFactsUrl(string)` - URL to stored facts file (JSON format)
- `WithSize(long)` - Size of facts data in bytes
- `WithProcessingDate(DateTime)` - When fact extraction occurred
- `WithConfidence(float)` - Overall extraction confidence (0.0-1.0)
- `AddProcessingNote(string)` - Add notes about extraction quality or coverage

### Fact Object Structure

```csharp
public class Fact
{
    public string factGuid { get; set; }                    // Unique identifier
    public string factDocumentReference { get; set; }       // Source document reference
    public string type { get; set; }                       // Category: diagnosis, lab, medication, etc.
    public string fact { get; set; }                       // Extracted fact text
    public string[] @ref { get; set; }                     // Location references in source
    public string timeRef { get; set; }                    // Temporal reference
    public string relevance { get; set; }                  // Clinical relevance explanation
}
```

## Example JSON Output

### OCR Document Reference

```json
{
  "resourceType": "DocumentReference",
  "id": "ocr-process-001",
  "meta": {
    "security": [
      {
        "system": "http://terminology.hl7.org/CodeSystem/v3-ActCode",
        "code": "AIAST",
        "display": "AI Assisted"
      }
    ]
  },
  "status": "current",
  "type": {
    "coding": [
      {
        "system": "http://loinc.org",
        "code": "18842-5",
        "display": "Discharge summary"
      }
    ],
    "text": "OCR Extracted Text"
  },
  "category": [
    {
      "coding": [
        {
          "system": "http://hl7.org/fhir/us/core/CodeSystem/us-core-documentreference-category",
          "code": "clinical-note",
          "display": "Clinical Note"
        }
      ]
    }
  ],
  "subject": {
    "reference": "Patient/patient-123",
    "display": "John Smith"
  },
  "date": "2024-01-15T10:30:00Z",
  "author": [
    {
      "reference": "Device/ocr-engine-v2",
      "display": "Advanced OCR Engine v2.1"
    }
  ],
  "relatesTo": [
    {
      "code": "transforms",
      "target": {
        "reference": "DocumentReference/scan-001",
        "display": "Original Pathology Report Scan"
      }
    }
  ],
  "content": [
    {
      "attachment": {
        "contentType": "text/plain",
        "url": "https://bucket.s3.amazonaws.com/docs/patient-123/ocr-text-001.txt",
        "size": 15248,
        "title": "OCR Extracted Text"
      }
    }
  ],
  "extension": [
    {
      "url": "http://thirdopinion.ai/fhir/StructureDefinition/confidence",
      "valueDecimal": 0.94
    }
  ]
}
```

### Fact Extraction Document Reference

```json
{
  "resourceType": "DocumentReference",
  "id": "fact-extraction-001",
  "meta": {
    "security": [
      {
        "system": "http://terminology.hl7.org/CodeSystem/v3-ActCode",
        "code": "AIAST",
        "display": "AI Assisted"
      }
    ]
  },
  "status": "current",
  "type": {
    "coding": [
      {
        "system": "http://loinc.org",
        "code": "67504-6",
        "display": "Evaluation and management note"
      }
    ],
    "text": "Clinical Fact Extraction"
  },
  "category": [
    {
      "coding": [
        {
          "system": "http://hl7.org/fhir/us/core/CodeSystem/us-core-documentreference-category",
          "code": "clinical-note",
          "display": "Clinical Note"
        }
      ]
    }
  ],
  "subject": {
    "reference": "Patient/patient-123",
    "display": "John Smith"
  },
  "date": "2024-01-15T10:45:00Z",
  "author": [
    {
      "reference": "Device/fact-extractor-ai",
      "display": "Clinical Fact Extractor AI v3.0"
    }
  ],
  "relatesTo": [
    {
      "code": "transforms",
      "target": {
        "reference": "DocumentReference/ocr-text-001",
        "display": "OCR Extracted Text"
      }
    }
  ],
  "content": [
    {
      "attachment": {
        "contentType": "application/json",
        "url": "https://bucket.s3.amazonaws.com/facts/patient-123/facts-001.json",
        "size": 2048,
        "title": "Extracted Clinical Facts"
      }
    }
  ],
  "extension": [
    {
      "url": "https://thirdopinion.io/clinical-fact",
      "extension": [
        {
          "url": "factGuid",
          "valueString": "fact-001"
        },
        {
          "url": "factDocumentReference",
          "valueString": "DocumentReference/ocr-text-001"
        },
        {
          "url": "type",
          "valueString": "diagnosis"
        },
        {
          "url": "fact",
          "valueString": "Adenocarcinoma of prostate"
        },
        {
          "url": "ref",
          "valueString": "Line 5"
        },
        {
          "url": "timeRef",
          "valueString": "2024-01-15"
        },
        {
          "url": "relevance",
          "valueString": "Primary diagnosis for treatment planning"
        }
      ]
    },
    {
      "url": "http://thirdopinion.ai/fhir/StructureDefinition/confidence",
      "valueDecimal": 0.89
    }
  ]
}
```

## AWS Integration

### S3 Content Storage

Both builders support AWS S3 integration for storing large content:

```csharp
// OCR text storage
var ocrDoc = new OcrDocumentReferenceBuilder(config)
    .WithContentText(extractedText)
    .WithContentUrl("https://clinical-docs.s3.amazonaws.com/ocr/text-001.txt")
    .WithSize(textBytes.Length)
    .Build();

// Facts JSON storage
var factsDoc = new FactExtractionDocumentReferenceBuilder(config)
    .WithExtractedFacts(facts)
    .WithFactsUrl("https://clinical-docs.s3.amazonaws.com/facts/facts-001.json")
    .WithSize(factsJsonBytes.Length)
    .Build();
```

### Lambda Function Integration

```csharp
public class DocumentProcessingHandler
{
    private readonly AiInferenceConfiguration _config;

    public async Task<APIGatewayProxyResponse> ProcessOcrAsync(APIGatewayProxyRequest request)
    {
        // Extract text from uploaded document
        var extractedText = await _ocrService.ProcessDocumentAsync(documentUrl);

        // Store extracted text in S3
        var s3Url = await _s3Service.UploadTextAsync(extractedText, bucketName, keyName);

        // Create FHIR DocumentReference
        var ocrDocument = new OcrDocumentReferenceBuilder(_config)
            .WithPatient(request.PathParameters["patientId"])
            .WithDevice("Device/aws-textract")
            .WithSourceDocument(request.PathParameters["sourceDocId"])
            .WithContentText(extractedText)
            .WithContentUrl(s3Url)
            .WithSize(Encoding.UTF8.GetByteCount(extractedText))
            .WithConfidence(ocrConfidence)
            .Build();

        return new APIGatewayProxyResponse
        {
            StatusCode = 200,
            Body = ocrDocument.ToJson()
        };
    }
}
```

## Validation

### OCR Document Validation

The `OcrDocumentReferenceBuilder` performs validation:

- **Patient reference** must be provided
- **Device reference** must be provided
- **Source document** must be provided
- **Content text** cannot be empty
- **Confidence** must be between 0.0 and 1.0 if provided
- **Size** must be positive if provided

### Fact Extraction Validation

The `FactExtractionDocumentReferenceBuilder` performs validation:

- **Patient reference** must be provided
- **Device reference** must be provided
- **Source document** must be provided
- **Facts array** cannot be empty
- **Each fact** must have valid factGuid, type, and fact
- **Confidence** must be between 0.0 and 1.0 if provided

### Error Handling

```csharp
try
{
    var document = builder.Build();
}
catch (InvalidOperationException ex)
{
    // Handle missing required fields
    _logger.LogError("Missing required field: {Message}", ex.Message);
}
catch (ArgumentException ex)
{
    // Handle invalid parameter values
    _logger.LogError("Invalid parameter: {Message}", ex.Message);
}
```

## Document Processing Workflow

### Complete OCR + Fact Extraction Pipeline

```csharp
// Step 1: OCR Processing
var ocrDocument = new OcrDocumentReferenceBuilder(config)
    .WithPatient(patientRef)
    .WithDevice(ocrDeviceRef)
    .WithSourceDocument(scanRef)
    .WithContentText(extractedText)
    .WithContentUrl(ocrS3Url)
    .WithConfidence(ocrConfidence)
    .Build();

// Step 2: Fact Extraction (using OCR output as source)
var factsDocument = new FactExtractionDocumentReferenceBuilder(config)
    .WithPatient(patientRef)
    .WithDevice(aiDeviceRef)
    .WithSourceDocument(ocrDocument.AsReference())
    .WithExtractedFacts(extractedFacts)
    .WithFactsUrl(factsS3Url)
    .WithConfidence(extractionConfidence)
    .Build();

// Step 3: Create observations/conditions based on facts
foreach (var fact in extractedFacts.Where(f => f.Type == "diagnosis"))
{
    // Use fact data to create clinical observations or conditions
}
```

## Best Practices

### Content Storage

1. **Large content** should be stored in S3 with URL references
2. **Small content** can be embedded directly in the resource
3. **Always specify content type** correctly (text/plain, application/json, etc.)
4. **Include size information** for content management

### Processing Metadata

1. **Always set confidence scores** for AI-processed content
2. **Add processing notes** to explain quality or limitations
3. **Include processing timestamps** for audit trails
4. **Reference source documents** to maintain lineage

### Error Recovery

1. **Validate inputs** before processing expensive operations
2. **Log processing details** for debugging failed extractions
3. **Handle partial failures** gracefully in batch processing
4. **Implement retry logic** for transient S3 upload failures

## Integration Notes

- Both builders extend `AiResourceBuilderBase<DocumentReference>` for consistent AI resource patterns
- All documents receive the AIAST (AI Assisted) security label automatically
- Generated resources are compatible with FHIR R4 and US Core profiles
- Builders follow the fluent interface pattern for method chaining
- Clinical fact extensions use the standardized `https://thirdopinion.io/clinical-fact` URL