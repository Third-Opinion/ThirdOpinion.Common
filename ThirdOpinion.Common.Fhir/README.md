# ThirdOpinion.Common.Fhir

A comprehensive library for building FHIR R4 resources representing AI-generated clinical inferences in prostate cancer trial eligibility assessments.

## Overview

This library provides fluent builder classes that construct FHIR R4 resources for AI-generated clinical inferences. It follows the Builder pattern to provide type-safe, chainable construction of complex FHIR resources while ensuring proper validation and metadata application.

## Technology Stack

- **Language**: C# (.NET 8+)
- **FHIR SDK**: Firely SDK (Hl7.Fhir.R4)
- **Deployment**: AWS
- **Testing**: xUnit with Shouldly assertions

## Core Design Principles

### Builder Pattern Architecture
All helper classes follow the Builder pattern to provide fluent, type-safe construction of FHIR resources. Each method returns the builder instance for chaining, similar to LINQ's fluent API.

**Example usage pattern:**
```csharp
var observation = new AdtStatusObservationBuilder(config)
    .WithInferenceId("abc12345-6789-4def-0123-456789abcdef")
    .WithPatient(patientReference)
    .WithDevice(aiDeviceReference)
    .WithStatus(true, confidenceScore: 0.95f)
    .AddEvidence(medicationStatementRef, "Active MedicationStatement indicating ADT use")
    .AddEvidence(medicationRequestRef, "Current ADT prescription")
    .Build();
```

### Mandatory Metadata Pattern
Every AI-generated resource automatically includes:
1. **AI Security Label** (`AIAST` code)
2. **Inference GUID** (using `to.io-{GUID}` format)
3. **Model Version Tag** (configurable per AI model)

### Resource Reference Handling
All builders accept `ResourceReference` objects from Firely SDK and support:
- Pre-constructed references: `new ResourceReference("Patient/example")`
- References with display text: `new ResourceReference("Patient/example", "John Doe")`
- Type-safe reference construction from existing FHIR resources

## Project Structure

```
ThirdOpinion.Common.Fhir/
├── Builders/
│   ├── Base/                    # Core builder infrastructure
│   ├── Conditions/              # Condition resource builders
│   ├── Observations/            # Observation resource builders
│   ├── Documents/               # DocumentReference builders
│   ├── Devices/                 # Device resource builders
│   └── Provenance/              # Provenance resource builders
├── Configuration/               # AI inference configuration
├── Extensions/                  # FHIR extension helpers
├── Helpers/                     # Utility classes
├── Models/                      # Data models
└── README.md                    # This file
```

## Available Builders

### Base Infrastructure
- **[AiResourceBuilderBase&lt;T&gt;](./Builders/Base/README.md)** - Abstract base class for all AI resource builders
- **FhirIdGenerator** - Generates unique FHIR resource IDs
- **FhirCodingHelper** - Provides standardized clinical codes

### Condition Builders
- **[HsdmAssessmentConditionBuilder](./Builders/Conditions/README.md)** - HSDM assessments for prostate cancer hormone sensitivity

### Observation Builders
- **[AdtStatusObservationBuilder](./Builders/Observations/README.md)** - ADT therapy status detection
- **[PsaProgressionObservationBuilder](./Builders/Observations/README.md)** - PSA progression analysis
- **[RecistProgressionObservationBuilder](./Builders/Observations/README.md)** - RECIST 1.1 radiographic progression

### Document Builders
- **[OcrDocumentReferenceBuilder](./Builders/Documents/README.md)** - OCR text extraction documents
- **[FactExtractionDocumentReferenceBuilder](./Builders/Documents/README.md)** - Clinical fact extraction documents

### Device Builders
- **[AiDeviceBuilder](./Builders/Devices/README.md)** - AI/ML system device resources

### Provenance Builders
- **[AiProvenanceBuilder](./Builders/Provenance/README.md)** - Audit trail and lineage tracking

## Quick Start

### 1. Install Dependencies
```xml
<PackageReference Include="Hl7.Fhir.R4" Version="5.6.0" />
```

### 2. Configure AI Inference
```csharp
var config = AiInferenceConfiguration.CreateDefault();
// or
var config = new AiInferenceConfiguration
{
    CriteriaSystem = "https://thirdopinion.io/fhir/CodeSystem/criteria",
    DefaultVersion = "v2.1",
    OrganizationName = "ThirdOpinion.io"
};
```

### 3. Build FHIR Resources
```csharp
// Create an AI device
var device = new AiDeviceBuilder(config)
    .WithName("Prostate Cancer AI Classifier")
    .WithVersion("2.1.0")
    .WithModelType("neural-network")
    .Build();

// Create an observation
var observation = new AdtStatusObservationBuilder(config)
    .WithPatient("Patient/123")
    .WithDevice(device.AsReference())
    .WithStatus(true, 0.95f)
    .Build();
```

## Configuration

### AiInferenceConfiguration
Central configuration class that provides:
- **CriteriaSystem**: Base URI for assessment criteria codes
- **DefaultVersion**: Default version for AI models
- **OrganizationName**: Organization identifier for provenance
- **ValidationRules**: Custom validation settings

### Environment Variables
- `FHIR_BASE_URL`: Base URL for FHIR server
- `AI_MODEL_VERSION`: Default AI model version
- `ORGANIZATION_ID`: Organization identifier

## Validation

All builders perform strict validation:
- **Required fields** must be set before calling `Build()`
- **Resource references** are validated for proper format
- **Clinical codes** are checked against known code systems
- **Data types** are validated for FHIR compliance

### Error Handling
```csharp
try
{
    var observation = builder.Build();
}
catch (InvalidOperationException ex)
{
    // Handle missing required fields
}
catch (ArgumentException ex)
{
    // Handle invalid parameter values
}
```

## Clinical Codes

The library provides standardized clinical codes through `FhirCodingHelper`:

### SNOMED CT Codes
- `1197209002` - Castration-sensitive prostate cancer
- `445848006` - Castration-resistant prostate cancer
- `277022003` - Progressive disease
- `359746009` - Stable disease

### LOINC Codes
- `21889-1` - Cancer disease status
- `97509-4` - PSA progression
- `33747-0` - Prostate specific antigen measurement

### ICD-10 Codes
- `Z19.1` - Hormone sensitive malignancy status
- `Z19.2` - Hormone resistant malignancy status
- `R97.21` - Rising PSA following treatment

## Testing

The library includes comprehensive unit tests:
- **Builder validation tests** - Ensure proper error handling
- **FHIR compliance tests** - Validate generated resources
- **Integration tests** - Test complete workflows
- **Performance tests** - Measure builder efficiency

```bash
# Run all tests
dotnet test

# Run specific test category
dotnet test --filter "Category=BuilderTests"
```

## AWS Integration

### Lambda Functions
```csharp
public class InferenceHandler
{
    private readonly AiInferenceConfiguration _config;

    public async Task<APIGatewayProxyResponse> HandleAsync(APIGatewayProxyRequest request)
    {
        var builder = new AdtStatusObservationBuilder(_config);
        var observation = builder
            .WithPatient(request.PathParameters["patientId"])
            .WithStatus(/* AI inference result */)
            .Build();

        return new APIGatewayProxyResponse
        {
            StatusCode = 200,
            Body = observation.ToJson()
        };
    }
}
```

### S3 Integration
Large clinical documents can be stored in S3 and referenced via URLs:
```csharp
var docBuilder = new OcrDocumentReferenceBuilder(config)
    .WithContentUrl("https://bucket.s3.amazonaws.com/docs/patient-123/scan.pdf")
    .WithSize(2048576) // 2MB
    .Build();
```

## Implementation Phases

### Phase 1: Core Infrastructure ✅
- AiResourceBuilderBase
- FhirIdGenerator, FhirCodingHelper
- AiInferenceConfiguration
- Unit tests for base classes

### Phase 2: Observation and Condition Builders ✅
- AdtStatusObservationBuilder
- HsdmAssessmentConditionBuilder
- PsaProgressionObservationBuilder
- Integration tests

### Phase 3: Advanced Builders ✅
- RecistProgressionObservationBuilder
- AiDeviceBuilder
- AiProvenanceBuilder
- Document processing builders

### Phase 4: Document Processing 🔄
- Enhanced OCR integration
- Fact extraction pipelines
- Multi-document workflows

## Contributing

### Coding Standards
- Follow C# naming conventions
- Use XML documentation comments
- Include comprehensive unit tests
- Validate FHIR compliance

### Adding New Builders
1. Extend `AiResourceBuilderBase<T>`
2. Implement required validation
3. Add comprehensive unit tests
4. Update documentation
5. Add examples to README

## Support

For questions and support:
- **Documentation**: See individual builder README files
- **Issues**: Report on project issue tracker
- **Examples**: See `Examples/` directory for sample usage

## License

Copyright (c) ThirdOpinion.io. All rights reserved.