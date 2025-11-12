# ThirdOpinion.Common

A comprehensive .NET library providing common utilities and AWS service integrations for ThirdOpinion applications.

## Features

### AWS Service Integration

- **Amazon S3**: File storage and retrieval utilities
- **Amazon DynamoDB**: Repository patterns and type converters
- **Amazon SQS**: Message queue management and handlers
- **Amazon Cognito**: Authentication and authorization utilities

### FHIR Resource Builders

- **AI-Assisted FHIR Resource Builders**: Type-safe fluent builders for creating FHIR R4 resources with AI inference metadata
- **Observation Builders**: RadiographicObservationBuilder, PsaProgressionObservationBuilder, AdtStatusObservationBuilder
- **Condition Builders**: HsdmAssessmentConditionBuilder
- **Base Builder Pattern**: Unified base class (AiResourceBuilderBase) with CRTP pattern for type-safe method chaining

### Utilities

- String extensions and manipulations
- Patient matching algorithms
- Common data models and helpers

## Installation

```bash
dotnet add package ThirdOpinion.Common
```

## Usage

### AWS Services

Configure AWS services in your `appsettings.json`:

```json
{
  "AWS": {
    "Region": "us-east-2"
  }
}
```

Register services in your DI container:

```csharp
services.AddAws();
services.AddDynamoDb();
services.AddS3Storage();
services.AddSqsMessageQueue();
services.AddCognito();
```

### Examples

#### S3 Storage

```csharp
public class FileService
{
    private readonly IS3StorageService _s3Service;

    public FileService(IS3StorageService s3Service)
    {
        _s3Service = s3Service;
    }

    public async Task UploadFileAsync(string bucketName, string key, Stream content)
    {
        await _s3Service.UploadFileAsync(bucketName, key, content);
    }
}
```

#### DynamoDB Repository

```csharp
public class UserRepository : IDynamoDbRepository<User>
{
    private readonly IDynamoDbRepository<User> _repository;

    public UserRepository(IDynamoDbRepository<User> repository)
    {
        _repository = repository;
    }

    public async Task<User> GetUserAsync(string userId)
    {
        return await _repository.GetAsync(userId);
    }
}
```

#### SQS Message Queue

```csharp
public class NotificationService
{
    private readonly ISqsMessageQueue _messageQueue;

    public NotificationService(ISqsMessageQueue messageQueue)
    {
        _messageQueue = messageQueue;
    }

    public async Task SendNotificationAsync<T>(string queueUrl, T message)
    {
        await _messageQueue.SendMessageAsync(queueUrl, message);
    }
}
```

### FHIR Resource Builders

The library provides a comprehensive set of builders for creating FHIR R4 resources with AI inference metadata. All builders inherit from `AiResourceBuilderBase<T, TBuilder>` which uses the CRTP (Curiously Recurring Template Pattern) for type-safe fluent method chaining.

#### Base Builder Pattern

The `AiResourceBuilderBase` class provides common functionality for all FHIR resource builders:

**Required Methods:**
- `WithPatient(string patientId)` - Sets the patient reference (required)
- `WithDevice(string deviceId)` - Sets the AI device reference (required)

**Optional Common Methods:**
- `WithFhirResourceId(string id)` - Sets a custom FHIR resource ID (auto-generated if not provided)
- `WithCriteria(string id, string display)` - Sets the assessment criteria used
- `WithConfidence(float confidence)` - Sets the AI confidence score (0.0 to 1.0)
- `AddNote(string noteText)` - Adds narrative notes to the resource
- `WithFocus(params ResourceReference[] focuses)` - Sets the conditions/lesions being assessed
- `AddEvidence(string referenceString, string? display)` - Adds supporting evidence references
- `AddDerivedFrom(string reference, string? display)` - Adds source document references

**Validation:**
- `PatientReference` and `DeviceReference` are required and validated before building
- All other fields are optional and can be validated by derived classes as needed

#### Radiographic Observation Builder

Creates FHIR Observation resources for radiographic progression assessment supporting RECIST 1.1, PCWG3, and Observed standards.

```csharp
using ThirdOpinion.Common.Fhir.Builders.Observations;
using ThirdOpinion.Common.Fhir.Configuration;
using ThirdOpinion.Common.Fhir.Models;

var config = new AiInferenceConfiguration
{
    CriteriaSystem = "http://thirdopinion.ai/fhir/CodeSystem/assessment-criteria"
};

var observation = new RadiographicObservationBuilder(config)
    .WithPatient("patient-123")
    .WithDevice("device-ai-v1")
    .WithStandard(ProgressionStandard.PCWG3)
    .WithEffectiveDate(DateTime.Now)
    .WithFocus(new ResourceReference { Reference = "Condition/tumor-1" })
    .AddNote("Progressive disease per PCWG3 criteria")
    .WithOverallResult(
        code: "PD",
        display: "Progressive Disease",
        system: "http://example.org/progression"
    )
    .AddBoneProgressionComponent(
        code: "new-bone-lesions",
        display: "New bone lesions detected",
        value: true
    )
    .AddEvidence("DocumentReference/scan-123", "CT scan from 2024-01-15")
    .WithConfidence(0.92f)
    .Build();
```

**Key Features:**
- Supports multiple progression standards (RECIST 1.1, PCWG3, Observed)
- Type-safe component builders for standard-specific assessments
- Focus references link to conditions/lesions being assessed
- Notes stored in top-level `note[].text` structure
- Evidence references support clinical documentation

#### PSA Progression Observation Builder

Creates FHIR Observation resources for PSA (Prostate-Specific Antigen) progression assessment.

```csharp
var observation = new PsaProgressionObservationBuilder(config)
    .WithPatient("patient-123")
    .WithDevice("device-ai-v1")
    .WithStandard(PsaProgressionStandard.PCWG3)
    .WithEffectiveDate(DateTime.Now)
    .WithFocus(new ResourceReference { Reference = "Condition/prostate-cancer-1" })
    .AddNote("PSA progression per PCWG3 criteria")
    .WithOverallResult(
        code: "progression",
        display: "PSA Progression",
        system: "http://thirdopinion.ai/fhir/CodeSystem/psa-assessment"
    )
    .AddPsaValueComponent(4.5m, DateTime.Now.AddMonths(-2))
    .AddPsaValueComponent(6.2m, DateTime.Now.AddMonths(-1))
    .AddPsaValueComponent(8.1m, DateTime.Now)
    .WithConfidence(0.95f)
    .Build();
```

#### HSDM Assessment Condition Builder

Creates FHIR Condition resources for Hormone Sensitivity Diagnosis Modifier (HSDM) assessment of Castration-Sensitive Prostate Cancer.

```csharp
using ThirdOpinion.Common.Fhir.Builders.Conditions;

var condition = new HsdmAssessmentConditionBuilder(config)
    .WithPatient("patient-123")
    .WithDevice("device-ai-v1")
    .WithFocus("condition-prostate-cancer", "Primary prostate cancer condition")
    .WithHSDMResult(HsdmAssessmentConditionBuilder.HsdmResults.MetastaticCastrationSensitive)
    .AddFactEvidence(
        new Fact { type = "metastasis", value = "present", confidence = 0.95f },
        new Fact { type = "castration-status", value = "sensitive", confidence = 0.88f }
    )
    .AddNote("Patient presents with metastatic disease and hormone-sensitive markers")
    .WithEffectiveDate(DateTime.Now)
    .WithConfidence(0.91f)
    .Build();
```

**HSDM Result Values:**
- `HsdmResults.NonMetastaticBiochemicalRelapse` - Non-metastatic CSPC with biochemical relapse
- `HsdmResults.MetastaticCastrationSensitive` - Metastatic castration-sensitive (mCSPC)
- `HsdmResults.MetastaticCastrationResistant` - Metastatic castration-resistant (mCRPC)

#### Builder Pattern Benefits

The refactored builder pattern provides:

1. **DRY Principle**: Common methods (`WithFocus`, `AddEvidence`, `AddNote`) consolidated in base class
2. **Type Safety**: CRTP pattern ensures fluent methods return the correct derived builder type
3. **Consistency**: All builders share the same common API for patient, device, notes, focus, and evidence
4. **Maintainability**: Changes to common functionality only need to be made in one place
5. **Validation**: Required fields (patient, device) validated automatically; derived classes can add specific validations
6. **FHIR Compliance**: Resources include AIAST security labels and proper FHIR R4 structure

## Requirements

- .NET 8.0 or later
- AWS credentials configured (via AWS CLI, environment variables, or IAM roles)

## License

MIT License

## Contributing

Please refer to the project's contribution guidelines.