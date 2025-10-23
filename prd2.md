# FHIR R4 AI-Generated Prostate Cancer Clinical Inferences - Implementation PRD

## Overview
This PRD defines the implementation requirements for C# helper classes using Firely SDK to construct FHIR R4 resources for AI-generated clinical inferences in prostate cancer trial eligibility assessments.

## Technology Stack
- **Language**: C# (.NET 6+)
- **FHIR SDK**: Firely SDK (formerly FHIR .NET API)
- **Package**: `Hl7.Fhir.R4` NuGet package
- **Deployment**: AWS
- **Projects** : In the solution ThirdOpinion.Common create a library project ThirdOpinion.Common.Fhir and ThirdOpinion.Common.Fhir.UnitTests(xunit and shoudly)

## Core Design Principles

### Builder Pattern Architecture
All helper classes follow the Builder pattern to provide fluent, type-safe construction of FHIR resources. Think of builders like LINQ's fluent API - each method returns the builder instance for chaining.

**Example usage pattern:**
```csharp
var observation = new AdtStatusObservationBuilder()
    .WithInferenceGuid("abc12345-6789-4def-0123-456789abcdef")
    .WithPatient(patientReference)
    .WithDevice(aiDeviceReference)
    .WithStatus(true, confidenceScore: 0.95m)
    .AddEvidence(medicationStatementRef, "Active MedicationStatement indicating ADT use")
    .AddEvidence(medicationRequestRef, "Current ADT prescription")
    .Build();
```

### Resource Reference Handling
All builders accept `ResourceReference` objects from Firely SDK. The implementation must support:
- Pre-constructed references: `new ResourceReference("Patient/example")`
- References with display text: `new ResourceReference("Patient/example", "John Doe")`
- Type-safe reference construction from existing FHIR resources

### Mandatory Metadata Pattern
Every AI-generated resource requires three core elements automatically added by builders:
1. **AI Security Label** (`AIAST` code)
2. **Inference GUID** (using `to.io-{GUID}` format)
3. **Model Version Tag** (configurable per AI model)

## Implementation Requirements

### 1. Base Infrastructure

#### `AiResourceBuilderBase<T>` Abstract Class
Provides common functionality for all AI resource builders.

**Requirements:**
- Generic type parameter `T` constrained to `Resource`
- Auto-generates inference IDs if not explicitly provided
- Applies AIAST security label automatically
- Thread-safe ID generation
- Validation before Build() returns resource

**Properties:**
```csharp
protected string InferenceId { get; set; }  // Auto-generated if not set
protected string CriteriaId { get; set; }   // Criteria ID like "adt-therapy-1234455-v1.0"
protected string CriteriaDisplay { get; set; }
protected List<ResourceReference> DerivedFromReferences { get; set; }
```

**Methods:**
```csharp
public TBuilder WithInferenceId(string id)  // Optional - auto-generates if not called
public TBuilder WithCriteria(string criteriaId, string display)  // Method coding
protected string EnsureInferenceId()  // Internal - generates ID if not set
public abstract T Build()
```

**Auto-generation behavior:**
- If `WithInferenceId()` is not called, the builder automatically generates an ID using `FhirIdGenerator.GenerateInferenceId()`
- Format: `to.ai-inference-{GUID}` (e.g., `to.ai-inference-a1b2c3d4-e5f6-7890-abcd-ef1234567890`)
- For documents: `to.ai-inference-doc-{GUID}` or `to.ai-inference-facts-{GUID}`
- For provenance: `to.io-prov-{GUID}`
- IDs are generated at Build() time to ensure uniqueness

**Example usage with explicit ID:**
```csharp
var obs = new AdtStatusObservationBuilder()
    .WithInferenceId("to.ai-inference-1")  // Explicit ID
    .WithPatient(patientRef)
    .Build();
```

**Example usage with auto-generated ID:**
```csharp
var obs = new AdtStatusObservationBuilder()
    // WithInferenceId() omitted - will auto-generate
    .WithPatient(patientRef)
    .Build();
// Results in ID like: to.ai-inference-a1b2c3d4-e5f6-7890-abcd-ef1234567890
```

#### `FhirIdGenerator` Static Helper
Generates consistent IDs following simplified patterns with auto-generation support.

**Requirements:**
- Static methods for generating IDs with standard prefixes
- GUID-based ID generation for uniqueness
- Support for custom ID prefixes

**Methods:**
```csharp
public static string GenerateInferenceId() // Returns "to.ai-inference-{GUID}"
public static string GenerateInferenceId(int sequenceNumber) // Returns "to.ai-inference-{n}" for sequential IDs
public static string GenerateResourceId(string prefix, string guid) // Returns "{prefix}-{guid}"
public static string GenerateProvenanceId() // Returns "to.io-prov-{GUID}"
public static string GenerateDocumentId(string type) // Returns "to.ai-inference-{type}-{GUID}"
```

**ID Format Examples:**
```
Observations:        to.ai-inference-a1b2c3d4-e5f6-7890-abcd-ef1234567890
Provenance:          to.io-prov-a1b2c3d4-e5f6-7890-abcd-ef1234567890
Documents (OCR):     to.ai-inference-doc-a1b2c3d4-e5f6-7890-abcd-ef1234567890
Documents (Facts):   to.ai-inference-facts-a1b2c3d4-e5f6-7890-abcd-ef1234567890
```

**Usage:**
```csharp
// Auto-generate ID
var id = FhirIdGenerator.GenerateInferenceId(); // "to.ai-inference-{GUID}"

// Sequential ID
var id = FhirIdGenerator.GenerateInferenceId(1); // "to.ai-inference-1"

// Custom prefix
var id = FhirIdGenerator.GenerateDocumentId("ocr"); // "to.ai-inference-ocr-{GUID}"
```

#### `FhirCodingHelper` Static Helper
Provides strongly-typed coding constants and factory methods.

**Requirements:**
- Constants for all SNOMED, ICD-10, LOINC codes in schema
- Factory methods for CodeableConcept construction
- Validation of code systems

**Example constants:**
```csharp
public static class SnomedCodes
{
    public const string AdtTherapy = "413712001";
    public const string CastrationSensitive = "1197209002";
    public const string ProgressiveDisease = "277022003";
    public const string AiAlgorithm = "706689003";
}

public static class Icd10Codes
{
    public const string ProstateCancer = "C61";
    public const string HormoneSensitiveStatus = "Z19.1";
    public const string HormoneResistantStatus = "Z19.2";
    public const string RisingPsaPostTreatment = "R97.21";
}
```

### 2. AI Device Builder

#### `AiDeviceBuilder` Class
Constructs Device resources representing AI inference engines.

**Requirements:**
- Inherits from `AiResourceBuilderBase<Device>`
- Support for flexible property additions (any key-value pairs)
- Simplified device naming with type code
- Manufacturer and version configuration

**Key methods:**
```csharp
public AiDeviceBuilder WithModelName(string name, string typeCode)  // typeCode like "trail-match-ai"
public AiDeviceBuilder WithManufacturer(string manufacturer)
public AiDeviceBuilder WithVersion(string version)
public AiDeviceBuilder AddProperty(string propertyName, Quantity value)  // Flexible properties
public AiDeviceBuilder AddProperty(string propertyName, decimal value, string unit)
```

**Example usage:**
```csharp
var device = new AiDeviceBuilder()
    .WithModelName("Trial Eligibility AI", "trail-match-ai")
    .WithManufacturer("ThirdOpinion.io AI Lab")
    .WithVersion("1.0.0")
    .AddProperty("Model Accuracy", 0.94m, "proportion")
    .Build();
```

### 3. ADT Therapy Detection Builder

#### `AdtStatusObservationBuilder` Class
Constructs Observation resources for ADT therapy status detection.

**Requirements:**
- Observation.status = "final"
- Observation.category = "therapy"
- SNOMED code 413712001 (ADT therapy)
- Support for active/inactive status values
- Multiple evidence references via derivedFrom
- Criteria-based method coding
- Auto-generates inference ID if not provided

**Key methods:**
```csharp
public AdtStatusObservationBuilder WithInferenceId(string id)  // OPTIONAL - auto-generates if omitted
public AdtStatusObservationBuilder WithPatient(ResourceReference patientRef)
public AdtStatusObservationBuilder WithDevice(ResourceReference deviceRef)
public AdtStatusObservationBuilder WithCriteria(string criteriaId, string display, string description)
public AdtStatusObservationBuilder WithStatus(bool isReceivingAdt)
public AdtStatusObservationBuilder AddEvidence(ResourceReference reference, string displayText = null)
public AdtStatusObservationBuilder WithEffectiveDate(DateTime dateTime)
public AdtStatusObservationBuilder WithTreatmentStartDate(DateTime treatmentStartDate, string medicationReferenceId, string displayText)
public AdtStatusObservationBuilder AddNote(string noteText)
```

**WithTreatmentStartDate Method:**
The `WithTreatmentStartDate` method adds a specialized component to track when ADT treatment began, including medication reference information.

**Parameters:**
- `treatmentStartDate` (DateTime): The date when ADT treatment started
- `medicationReferenceId` (string): Reference to the MedicationReference resource (e.g., "MedicationReference/med-123")
- `displayText` (string): Human-readable description of the treatment start (e.g., "ADT treatment started on 2025-01-01 with Zoladex 20 mg")

**Generated FHIR Component:**
```json
{
  "component": [
    {
      "code": {
        "coding": [
          {
            "system": "https://thirdopinion.io/result-code",
            "code": "treatmentStartDate_v1",
            "display": "The date treatment started"
          }
        ],
        "text": "{displayText}"
      },
      "valueDateTime": "{treatmentStartDate}",
      "extension": [
        {
          "url": "https://thirdopinion.io/fhir/StructureDefinition/source-medication-reference",
          "valueReference": {
            "reference": "{medicationReferenceId}",
            "display": "The MedicationReference used in the analysis."
          }
        }
      ]
    }
  ]
}
```

**Example usage with explicit ID:**
```csharp
var observation = new AdtStatusObservationBuilder()
    .WithInferenceId("to.ai-inference-1")
    .WithPatient(new ResourceReference("Patient/example"))
    .WithDevice(new ResourceReference("Device/to.io-trial-eligibility-ai-v1"))
    .WithCriteria("adt-therapy-1234455-v1.0", 
        "ThirdOpinion.io ADT Therapy(ID:1234455) v1.2.0",
        "ThirdOpinion.io on ADT therapy assessment")
    .WithStatus(true)
    .AddEvidence(new ResourceReference("MedicationRequest/a-15454.med-192905", "Eligard 20mg 2025-10-11"))
    .WithEffectiveDate(new DateTime(2025, 9, 30, 10, 30, 0, DateTimeKind.Utc))
    .WithTreatmentStartDate(new DateTime(2025, 1, 1), "MedicationReference/some-medicationreference-3", "ADT treatment started on 2025-01-01 with Zoladex 20 mg")
    .Build();
```

**Example usage with auto-generated ID:**
```csharp
var observation = new AdtStatusObservationBuilder()
    // WithInferenceId() omitted - auto-generates to.ai-inference-{GUID}
    .WithPatient(new ResourceReference("Patient/example"))
    .WithDevice(new ResourceReference("Device/to.io-trial-eligibility-ai-v1"))
    .WithCriteria("adt-therapy-1234455-v1.0", 
        "ThirdOpinion.io ADT Therapy(ID:1234455) v1.2.0",
        "ThirdOpinion.io on ADT therapy assessment")
    .WithStatus(true)
    .AddEvidence(new ResourceReference("MedicationRequest/a-15454.med-192905"))
    .WithTreatmentStartDate(new DateTime(2025, 1, 1), "MedicationReference/med-ref-123", "ADT treatment started on 2025-01-01 with Lupron 22.5 mg")
    .Build();
```

### 4. CSPC Assessment Builder (UPDATED)

#### `CspcAssessmentObservationBuilder` Class
Constructs Observation resources for castration sensitivity assessment.

**CRITICAL CHANGE**: This builder creates an **Observation** that references an existing Condition via the `focus` field, NOT a new Condition resource.

**Requirements:**
- Observation.status = "final"
- Observation.category = "exam"
- LOINC code 21889-1 (Cancer disease status)
- SNOMED code 1197209002 (Castration-sensitive) in valueCodeableConcept
- ICD-10 code Z19.1 (Hormone sensitive malignancy status) in valueCodeableConcept
- **MANDATORY**: focus field must reference existing prostate cancer Condition
- Criteria-based method coding
- Multiple evidence types (testosterone, PSA, ADT status)

**Key methods:**
```csharp
public CspcAssessmentObservationBuilder WithInferenceId(string id)
public CspcAssessmentObservationBuilder WithPatient(ResourceReference patientRef)
public CspcAssessmentObservationBuilder WithDevice(ResourceReference deviceRef)
public CspcAssessmentObservationBuilder WithFocus(ResourceReference existingConditionRef)  // REQUIRED
public CspcAssessmentObservationBuilder WithCriteria(string criteriaId, string display, string description)
public CspcAssessmentObservationBuilder WithCastrationSensitive(bool isSensitive)
public CspcAssessmentObservationBuilder AddEvidence(ResourceReference reference, string displayText = null)
public CspcAssessmentObservationBuilder WithEffectiveDate(DateTime dateTime)
public CspcAssessmentObservationBuilder WithInterpretation(string interpretationText)
public CspcAssessmentObservationBuilder AddNote(string noteText)
```

**Example usage:**
```csharp
var assessment = new CspcAssessmentObservationBuilder()
    .WithInferenceId("to.ai-inference-1")
    .WithPatient(new ResourceReference("Patient/example"))
    .WithDevice(new ResourceReference("Device/to.io-trial-eligibility-ai-v2"))
    .WithFocus(new ResourceReference("Condition/prostate-cancer-primary-001", "Primary prostate cancer diagnosis"))
    .WithCriteria("cspc-assessment-1234455-v1.0", 
        "ThirdOpinion.io CSPC Assessment(ID:1234455) v1.1",
        "ThirdOpinion.io AI inference for CSPC based on hormone levels, PSA kinetics, and treatment response")
    .WithCastrationSensitive(true)
    .AddEvidence(new ResourceReference("Observation/testosterone-level-001"), "Testosterone 15 ng/dL (castrate level)")
    .AddEvidence(new ResourceReference("Observation/psa-response-001"), "PSA decreased 80% on ADT")
    .AddEvidence(new ResourceReference("Observation/to.io-adt-status-obs-001"), "Active ADT therapy")
    .WithInterpretation("Disease responding to ADT, hormone-sensitive")
    .WithEffectiveDate(DateTime.UtcNow)
    .AddNote("Classification derived from testosterone <50 ng/dL during ADT with PSA decline >50%")
    .Build();
```

**Validation requirements:**
- Build() must throw `InvalidOperationException` if focus is not set
- Build() must validate that focus references a Condition resource
- Must apply both SNOMED 1197209002 AND ICD-10 Z19.1 codes in valueCodeableConcept

### 5. PSA Progression Assessment Builder

#### `PsaProgressionObservationBuilder` Class
Constructs Observation resources for PSA progression using either ThirdOpinion.io or PCWG3 criteria.

**Requirements:**
- Support for custom ThirdOpinion.io criteria
- Support for PCWG3 criteria
- Component elements for measurements and validity periods
- Multiple PSA value references via derivedFrom
- Detailed clinical analysis notes

**Key methods:**
```csharp
public PsaProgressionObservationBuilder WithInferenceId(string id)
public PsaProgressionObservationBuilder WithPatient(ResourceReference patientRef)
public PsaProgressionObservationBuilder WithDevice(ResourceReference deviceRef)
public PsaProgressionObservationBuilder WithFocus(ResourceReference conditionRef)
public PsaProgressionObservationBuilder WithCriteria(string criteriaId, string display, string description)
public PsaProgressionObservationBuilder AddPsaEvidence(ResourceReference observationRef, string displayText)
public PsaProgressionObservationBuilder WithProgression(bool hasProgressed)
public PsaProgressionObservationBuilder AddValidUntilComponent(DateTime validUntil)
public PsaProgressionObservationBuilder AddThresholdMetComponent(bool thresholdMet)
public PsaProgressionObservationBuilder AddDetailedAnalysisNote(string analysis)
public PsaProgressionObservationBuilder AddNote(string noteText)
```

**Example usage:**
```csharp
var progression = new PsaProgressionObservationBuilder()
    .WithInferenceId("to.ai-inference-1")
    .WithPatient(new ResourceReference("Patient/example"))
    .WithDevice(new ResourceReference("Device/to.io-trial-eligibility-ai-v2"))
    .WithFocus(new ResourceReference("Condition/to.io-prostate-cancer-cspc-001"))
    .WithCriteria("psa-progression-1234455-v1.2", 
        "ThirdOpinion.io PSA Progression Criteria ID:1234455 v1.2",
        "ThirdOpinion.io PSA progression criteria: ≥30% and ≥3 ng/mL increase from nadir, confirmed by second measurement ≥4 weeks later")
    .AddPsaEvidence(new ResourceReference("Observation/psa-2024-07-15"), "PSA nadir 5.2 ng/mL on 2024-07-15")
    .AddPsaEvidence(new ResourceReference("Observation/psa-2024-09-01"), "PSA 9.8 ng/mL on 2024-09-01 (first rise)")
    .AddPsaEvidence(new ResourceReference("Observation/psa-2024-09-29"), "PSA 11.8 ng/mL on 2024-09-29 (confirmatory, 4 weeks later)")
    .WithProgression(true)
    .AddValidUntilComponent(new DateTime(2025, 10, 28))
    .AddThresholdMetComponent(true)
    .AddNote("PSA progression confirmed per ThirdOpinion.io criteria: ≥30% and ≥3 ng/mL increase from nadir")
    .AddDetailedAnalysisNote(@"PSA Analysis - Prostate Cancer Treatment Response
Chronological PSA Values: 11/14/24: Total PSA = 85.23 ng/mL (SEVERELY ELEVATED)
Treatment Response: PSA decreased to 0.1 ng/mL (>99% reduction)
⚠️ CRITICAL DATA ISSUE: Two conflicting PSA values on 4/30/25 require investigation")
    .Build();
```

### 6. RECIST 1.1 Progression Builder

#### `RecistProgressionObservationBuilder` Class
Constructs Observation resources for radiographic progression per RECIST 1.1.

**Requirements:**
- LOINC code 21976-6 (Tumor response)
- NCI Thesaurus codes for RECIST 1.1
- Component elements for SLD, nadir, percent change
- References to ImagingStudy resources
- Support for new lesion detection
- Criteria-based method coding

**Key methods:**
```csharp
public RecistProgressionObservationBuilder WithInferenceId(string id)
public RecistProgressionObservationBuilder WithPatient(ResourceReference patientRef)
public RecistProgressionObservationBuilder WithDevice(ResourceReference deviceRef)
public RecistProgressionObservationBuilder WithFocus(ResourceReference conditionRef)
public RecistProgressionObservationBuilder WithCriteria(string criteriaId, string display, string description)
public RecistProgressionObservationBuilder AddComponent(string codeText, Quantity valueQuantity)
public RecistProgressionObservationBuilder AddComponent(string codeText, bool valueBoolean)
public RecistProgressionObservationBuilder AddComponent(string codeText, CodeableConcept valueCodeableConcept)
public RecistProgressionObservationBuilder AddImagingStudy(ResourceReference imagingStudyRef, string displayText = null)
public RecistProgressionObservationBuilder AddRadiologyReport(ResourceReference documentRef, string displayText = null)
public RecistProgressionObservationBuilder WithRecistResponse(string nciCode, string display)
public RecistProgressionObservationBuilder AddBodySite(string snomedCode, string display)
public RecistProgressionObservationBuilder AddNote(string noteText)
```

**Example usage:**
```csharp
var recist = new RecistProgressionObservationBuilder()
    .WithInferenceId("to.ai-inference-1")
    .WithPatient(new ResourceReference("Patient/example"))
    .WithDevice(new ResourceReference("Device/to.io-trial-eligibility-ai-v2"))
    .WithFocus(new ResourceReference("Condition/to.io-prostate-cancer-cspc-001"))
    .WithCriteria("radiology-progression-1234455-v1.0",
        "ThirdOpinion.io RECIST 1.1 Progression Criteria ID:1234455 v1.0",
        "ThirdOpinion.io AI Response Evaluation Criteria in Solid Tumors version 1.1")
    .AddImagingStudy(new ResourceReference("ImagingStudy/ct-chest-abdomen-2025-09-25"), "CT Chest/Abdomen with contrast 2025-09-25")
    .AddRadiologyReport(new ResourceReference("DocumentReference/radiology-report-2025-09-25"), "Radiology report by Dr. Smith")
    .WithRecistResponse("C35571", "Progressive Disease")
    .AddBodySite("10200004", "Liver structure")
    .AddBodySite("39607008", "Lung structure")
    .AddComponent("Sum of target lesion diameters (SLD)", new Quantity { Value = 78, Unit = "mm", System = "http://unitsofmeasure.org", Code = "mm" })
    .AddComponent("New lesions detected", true)
    .AddNote("Progressive disease based on >20% increase in SLD from nadir (62→78mm, 25.8% increase)")
    .Build();
```

### 7. Provenance Builder

#### `AiProvenanceBuilder` Class
Constructs Provenance resources for audit trails of AI-generated resources. Supports optional log file references stored in S3.

**Requirements:**
- References target resource with version if available
- AI Device as "assembler" agent
- Organization as "author" agent
- Entity references for all source data
- Policy and reason codes
- Simplified identifier system
- Optional log file S3 reference for detailed audit trails

**Key methods:**
```csharp
public AiProvenanceBuilder WithProvenanceId(string id)  // e.g., "to.io-prov-1"
public AiProvenanceBuilder ForTarget(ResourceReference targetRef, string version = null)
public AiProvenanceBuilder WithOccurredDateTime(DateTime occurred)
public AiProvenanceBuilder WithAiDevice(ResourceReference deviceRef, string displayText = null)
public AiProvenanceBuilder WithOrganization(ResourceReference orgRef)
public AiProvenanceBuilder AddSourceEntity(ResourceReference entityRef, string displayText = null)
public AiProvenanceBuilder WithPolicy(string policyUrl)
public AiProvenanceBuilder WithReason(string reasonCode, string reasonText)
public AiProvenanceBuilder WithLogFileUrl(string s3Url)  // S3 reference to detailed log file
```

**Example usage with log file:**
```csharp
var provenance = new AiProvenanceBuilder()
    .WithProvenanceId("to.io-prov-1")
    .ForTarget(new ResourceReference("Observation/to.io-psa-progression-001/_history/1"))
    .WithOccurredDateTime(new DateTime(2025, 9, 30, 10, 30, 0, DateTimeKind.Utc))
    .WithAiDevice(new ResourceReference("Device/to.io-trial-eligibility-ai-v1"), "Prostate Cancer Trial Eligibility AI v1.0")
    .WithOrganization(new ResourceReference("Organization/thirdopinion-ai-lab"))
    .WithPolicy("https://thirdopinion.io/policies/ai-clinical-use-v1")
    .WithReason("TREAT", "Clinical trial eligibility assessment")
    .AddSourceEntity(new ResourceReference("Observation/psa-2024-07-15"), "PSA nadir 5.2 ng/mL")
    .AddSourceEntity(new ResourceReference("Observation/psa-2024-09-29"), "PSA 11.8 ng/mL (confirmatory)")
    .WithLogFileUrl("s3://thirdopinion-logs/inference/2025/09/30/inference-12345.log")
    .Build();
```

**Implementation notes:**
- Log file URL should be added as an entity with role "derivation"
- The entity should reference a DocumentReference that points to the S3 log file
- Alternatively, add as a signature element with reference to the log location
- S3 URLs should follow pattern: `s3://bucket-name/path/to/logfile.log`
- Log files typically contain: model parameters, input data checksums, processing timestamps, confidence scores, and decision traces

### 8. DocumentReference Builders

#### `OcrDocumentReferenceBuilder` Class
Creates DocumentReference for OCR-extracted text with relatesTo linking. Supports both inline text and S3 URL references for AWS Textract integration.

**Requirements:**
- Transforms relationship to original document
- AI Device as author
- Support for inline Base64-encoded text content OR S3 URL references
- Optional Textract Raw and Textract Simple JSON outputs as S3 URLs
- MIME type: text/plain;charset=utf-8 for inline text, or application/json for Textract outputs

**Key methods:**
```csharp
public OcrDocumentReferenceBuilder WithInferenceId(string id)
public OcrDocumentReferenceBuilder WithOriginalDocument(ResourceReference originalDocRef)
public OcrDocumentReferenceBuilder WithPatient(ResourceReference patientRef)
public OcrDocumentReferenceBuilder WithOcrDevice(ResourceReference deviceRef)
public OcrDocumentReferenceBuilder WithExtractedText(string plainText)  // Inline text (Base64-encoded)
public OcrDocumentReferenceBuilder WithExtractedTextUrl(string s3Url)  // S3 URL reference
public OcrDocumentReferenceBuilder WithTextractRawUrl(string s3Url)  // AWS Textract raw JSON output
public OcrDocumentReferenceBuilder WithTextractSimpleUrl(string s3Url)  // AWS Textract simplified JSON
public OcrDocumentReferenceBuilder WithDescription(string description)
```

**Example usage with inline text:**
```csharp
var ocrDoc = new OcrDocumentReferenceBuilder()
    .WithInferenceId("to.ai-inference-doc-1")
    .WithOriginalDocument(new ResourceReference("DocumentReference/scan-001"))
    .WithPatient(new ResourceReference("Patient/example"))
    .WithOcrDevice(new ResourceReference("Device/to.io-ocr-engine-v3"))
    .WithExtractedText("CT CHEST/ABDOMEN with Contrast...")  // Auto Base64-encodes
    .WithDescription("OCR-extracted text from radiology report")
    .Build();
```

**Example usage with S3 URLs:**
```csharp
var ocrDoc = new OcrDocumentReferenceBuilder()
    .WithInferenceId("to.ai-inference-doc-1")
    .WithOriginalDocument(new ResourceReference("DocumentReference/scan-001"))
    .WithPatient(new ResourceReference("Patient/example"))
    .WithOcrDevice(new ResourceReference("Device/to.io-ocr-engine-v3"))
    .WithExtractedTextUrl("s3://thirdopinion-ocr/patient-123/ocr-text.txt")
    .WithTextractRawUrl("s3://thirdopinion-ocr/patient-123/textract-raw.json")
    .WithTextractSimpleUrl("s3://thirdopinion-ocr/patient-123/textract-simple.json")
    .WithDescription("OCR outputs stored in S3")
    .Build();
```

**Implementation notes:**
- When using S3 URLs, set `attachment.url` instead of `attachment.data`
- Textract outputs should be added as separate content entries
- Builder should validate that either inline text OR URL is provided, not both
- S3 URLs should follow pattern: `s3://bucket-name/path/to/file`

#### `FactExtractionDocumentReferenceBuilder` Class
Creates DocumentReference for AI-extracted structured facts in JSON. Supports both inline JSON and S3 URL references for large fact sets.

**Requirements:**
- Transforms relationships to both OCR and original documents
- AI Device as author
- Support for inline Base64-encoded JSON content OR S3 URL references
- MIME type: application/json

**Key methods:**
```csharp
public FactExtractionDocumentReferenceBuilder WithInferenceId(string id)
public FactExtractionDocumentReferenceBuilder WithOriginalDocument(ResourceReference originalDocRef)
public FactExtractionDocumentReferenceBuilder WithOcrDocument(ResourceReference ocrDocRef)
public FactExtractionDocumentReferenceBuilder WithPatient(ResourceReference patientRef)
public FactExtractionDocumentReferenceBuilder WithExtractionDevice(ResourceReference deviceRef)
public FactExtractionDocumentReferenceBuilder WithFactsJson(object factsObject)  // Inline - Auto-serializes to JSON and Base64-encodes
public FactExtractionDocumentReferenceBuilder WithFactsJson(string jsonString)  // Inline - Base64-encodes
public FactExtractionDocumentReferenceBuilder WithFactsJsonUrl(string s3Url)  // S3 URL reference
public FactExtractionDocumentReferenceBuilder WithDescription(string description)
```

**Example usage with inline JSON:**
```csharp
var factsObject = new
{
    findings = new[]
    {
        new { concept = "New liver metastasis", snomed = "94222008", confidence = 0.92 }
    },
    protocol = "RECIST 1.1"
};

var factsDoc = new FactExtractionDocumentReferenceBuilder()
    .WithInferenceId("to.ai-inference-facts-1")
    .WithOriginalDocument(new ResourceReference("DocumentReference/scan-001"))
    .WithOcrDocument(new ResourceReference("DocumentReference/ocr-001"))
    .WithPatient(new ResourceReference("Patient/example"))
    .WithExtractionDevice(new ResourceReference("Device/to.io-nlp-v3"))
    .WithFactsJson(factsObject)  // Auto-serializes and Base64-encodes
    .WithDescription("Structured clinical facts")
    .Build();
```

**Example usage with S3 URL:**
```csharp
var factsDoc = new FactExtractionDocumentReferenceBuilder()
    .WithInferenceId("to.ai-inference-facts-1")
    .WithOriginalDocument(new ResourceReference("DocumentReference/scan-001"))
    .WithOcrDocument(new ResourceReference("DocumentReference/ocr-001"))
    .WithPatient(new ResourceReference("Patient/example"))
    .WithExtractionDevice(new ResourceReference("Device/to.io-nlp-v3"))
    .WithFactsJsonUrl("s3://thirdopinion-facts/patient-123/extracted-facts.json")
    .WithDescription("Structured clinical facts stored in S3")
    .Build();
```

**Implementation notes:**
- When using S3 URL, set `attachment.url` instead of `attachment.data`
- Builder should validate that either inline JSON OR URL is provided, not both
- For inline JSON, auto-serialize objects to JSON string then Base64-encode
- S3 URLs should follow pattern: `s3://bucket-name/path/to/file.json`

## Validation Requirements

### Pre-Build Validation
All builders must validate required fields before Build() returns:

**Common validations:**
- Inference GUID is set (auto-generate if missing)
- Patient reference is set
- Device reference is set
- Status/effective dates are valid

**Resource-specific validations:**
- `CspcAssessmentObservationBuilder`: Focus reference MUST be set
- `PsaProgressionObservationBuilder`: Must have nadir and current PSA values
- `RecistProgressionObservationBuilder`: Must have nadir and current SLD
- `AiProvenanceBuilder`: Must have at least one target and one agent

**Validation exceptions:**
```csharp
throw new InvalidOperationException("Patient reference is required");
throw new ArgumentException("Inference GUID must follow to.io-{GUID} format");
throw new InvalidOperationException("CSPC assessment requires focus reference to existing Condition");
```

## Configuration and Extensibility

### `AiInferenceConfiguration` Class
Provides configuration for AI model metadata and system URIs.

**Properties:**
```csharp
public string InferenceSystem { get; set; } = "https://thirdopinion.io/ai-inference";
public string CriteriaSystem { get; set; } = "https://thirdopinion.io/criteria";
public string ModelSystem { get; set; } = "https://thirdopinion.io/ai-models";
public string DocumentTrackingSystem { get; set; } = "https://thirdopinion.io/document-tracking";
public string ProvenanceSystem { get; set; } = "https://thirdopinion.io/provenance-tracking";
public string DefaultModelVersion { get; set; } = "trial-eligibility-v1.0";
public string OrganizationReference { get; set; } = "Organization/thirdopinion-ai-lab";
```

**Usage:**
```csharp
var config = new AiInferenceConfiguration
{
    DefaultModelVersion = "trial-eligibility-v2.0",
    OrganizationReference = "Organization/my-org"
};

var builder = new AdtStatusObservationBuilder(config);
```

## Error Handling

### Builder Exception Strategy
All builders follow consistent error handling:

**Exception types:**
- `ArgumentNullException`: Required reference parameter is null
- `ArgumentException`: Invalid value format (e.g., malformed GUID)
- `InvalidOperationException`: Missing required builder state (e.g., Build() before required methods called)
- `FhirResourceValidationException`: Firely SDK validation failures

**Example error messages:**
```csharp
// Good error messages
throw new InvalidOperationException(
    "CSPC assessment requires a focus reference to the existing prostate cancer Condition. " +
    "Call WithFocus() before Build()."
);

// Bad error messages
throw new Exception("Invalid state");  // ❌ Too vague
```

## Testing Requirements

### Unit Test Coverage
All builders require unit tests for:

1. **Happy path construction**: Valid resources with all required fields
2. **Mandatory field validation**: Build() fails when required fields missing
3. **Code system accuracy**: Correct SNOMED/ICD-10/LOINC codes applied
4. **AI metadata**: AIAST label and inference GUID always present
5. **Reference handling**: derivedFrom, focus, hasMember populated correctly
6. **Component calculation**: PSA progression auto-calculates components correctly

### Integration Test Scenarios
Test complete workflow scenarios:

1. **ADT Detection + CSPC Assessment + PSA Progression**: Full inference pipeline
2. **Document Processing**: Original → OCR → Fact Extraction with relatesTo links
3. **Provenance Tracking**: Every AI resource has valid Provenance
4. **Query Patterns**: Resources queryable by inference GUID, security label

## Code Organization

### Namespace Structure
```
ThirdOpinion.Fhir.AiInference
├── Builders
│   ├── Base
│   │   └── AiResourceBuilderBase.cs
│   ├── Observations
│   │   ├── AdtStatusObservationBuilder.cs
│   │   ├── CspcAssessmentObservationBuilder.cs
│   │   ├── PsaProgressionObservationBuilder.cs
│   │   └── RecistProgressionObservationBuilder.cs
│   ├── Documents
│   │   ├── OcrDocumentReferenceBuilder.cs
│   │   └── FactExtractionDocumentReferenceBuilder.cs
│   ├── AiDeviceBuilder.cs
│   └── AiProvenanceBuilder.cs
├── Configuration
│   └── AiInferenceConfiguration.cs
├── Helpers
│   ├── FhirIdGenerator.cs
│   └── FhirCodingHelper.cs
└── Enums
    ├── ProgressionCriteria.cs
    └── RecistResponse.cs
```

## Clinical Codes Quick Reference (C# Constants)

All codes available as constants in `FhirCodingHelper`:

```csharp
// SNOMED-CT
public const string SNOMED_ADT_THERAPY = "413712001";
public const string SNOMED_CASTRATION_SENSITIVE = "1197209002";
public const string SNOMED_PROGRESSIVE_DISEASE = "277022003";
public const string SNOMED_AI_ALGORITHM = "706689003";
public const string SNOMED_LIVER_STRUCTURE = "10200004";
public const string SNOMED_LUNG_STRUCTURE = "39607008";

// ICD-10
public const string ICD10_PROSTATE_CANCER = "C61";
public const string ICD10_HORMONE_SENSITIVE = "Z19.1";
public const string ICD10_HORMONE_RESISTANT = "Z19.2";
public const string ICD10_RISING_PSA = "R97.21";
public const string ICD10_ADT_LONG_TERM = "Z79.81";

// LOINC
public const string LOINC_CANCER_DISEASE_STATUS = "21889-1";
public const string LOINC_PSA_TOTAL = "2857-1";
public const string LOINC_PSA_ULTRASENSITIVE = "35741-8";
public const string LOINC_CANCER_PROGRESSION = "97509-4";
public const string LOINC_TUMOR_RESPONSE = "21976-6";

// NCI Thesaurus
public const string NCI_RECIST_11 = "C111544";
public const string NCI_PROGRESSIVE_DISEASE = "C35571";
```

## AWS Deployment Considerations

### Lambda Integration Pattern
Builders designed for serverless Lambda functions:

**Input:** FHIR Bundle of patient data from S3/DynamoDB
**Processing:** AI model inference + builder construction
**Output:** FHIR Bundle with AI-generated resources

**Memory optimization:**
- Builders are lightweight (no heavy caching)
- Lazy evaluation where possible
- Dispose of resources after Build()

**Cold start optimization:**
- Static helpers for code constants
- Minimal constructor work
- Configuration injectable

### S3 URL Support for Large Content

All document-related builders support both inline content (Base64-encoded) and S3 URL references for optimal storage and performance.

**When to use inline content:**
- Small documents (<100 KB)
- Frequently accessed content
- Content needed for immediate display

**When to use S3 URLs:**
- Large documents (>100 KB)
- OCR outputs from AWS Textract
- Detailed AI inference logs
- Fact extraction JSON with many findings
- Content accessed infrequently

**S3 URL patterns:**
```
OCR text:              s3://thirdopinion-ocr/patient-{id}/reports/{filename}.txt
Textract raw:          s3://thirdopinion-ocr/patient-{id}/reports/{filename}-textract-raw.json
Textract simple:       s3://thirdopinion-ocr/patient-{id}/reports/{filename}-textract-simple.json
Extracted facts:       s3://thirdopinion-facts/patient-{id}/reports/{filename}-facts.json
Inference logs:        s3://thirdopinion-logs/inference/{yyyy}/{mm}/{dd}/{inference-id}-execution.log
```

**Security considerations:**
- All S3 buckets should be private with appropriate IAM roles
- Use pre-signed URLs for temporary access if needed
- Consider encryption at rest (S3 SSE-KMS)
- Enable versioning for audit trails
- Set lifecycle policies for log retention

**FHIR resource patterns:**
- Inline: Set `attachment.data` with Base64-encoded content
- S3 URL: Set `attachment.url` with S3 URI
- Never set both data and url in the same attachment
- S3 URLs should be resolvable by authorized FHIR clients

## Implementation Phases

### Phase 1: Core Infrastructure
- AiResourceBuilderBase
- FhirIdGenerator
- FhirCodingHelper
- AiInferenceConfiguration
- Unit tests for base classes

### Phase 2: Observation Builders
- AdtStatusObservationBuilder
- CspcAssessmentObservationBuilder (with focus validation)
- PsaProgressionObservationBuilder
- Integration tests for observation workflow

### Phase 3: Advanced Builders
- RecistProgressionObservationBuilder
- AiDeviceBuilder
- AiProvenanceBuilder
- End-to-end integration tests

### Phase 4: Document Processing
- OcrDocumentReferenceBuilder
- FactExtractionDocumentReferenceBuilder
- Document transformation workflow tests

## Acceptance Criteria

### Definition of Done
A builder is complete when:

1. ✅ All required methods implemented
2. ✅ Validation logic enforces mandatory fields
3. ✅ Unit test coverage >90%
4. ✅ Integration test demonstrates FHIR-compliant resource
5. ✅ XML documentation on all public methods
6. ✅ Example usage in PR description
7. ✅ Tested with real Firely SDK serialization/deserialization

### Quality Gates
- All FHIR resources validate against R4 schema
- Inference GUIDs follow `to.io-{GUID}` pattern
- AIAST security label present on all AI resources
- derivedFrom chains are complete and valid
- No hardcoded strings (use FhirCodingHelper constants)

## Example FHIR Resources

This section provides complete JSON examples of all FHIR resources that the builders should produce. Use these as reference implementations and test fixtures.

### Example 1: AI Device Resource

```json
{
    "resourceType": "Device",
    "id": "to.io-trial-eligibility-ai-v1",
    "identifier": [
        {
            "system": "https://thirdopinion.io/ai-models",
            "value": "trial-eligibility-v1.0"
        }
    ],
    "status": "active",
    "manufacturer": "ThirdOpinion.io AI Lab",
    "deviceName": [
        {
            "name": "Trial Eligibility AI",
            "type": "trail-match-ai"
        }
    ],
    "type": {
        "coding": [
            {
                "system": "http://snomed.info/sct",
                "code": "706689003",
                "display": "Artificial intelligence algorithm"
            }
        ]
    },
    "version": [
        {
            "type": {
                "text": "Software Version"
            },
            "value": "1.0.0"
        }
    ],
    "property": []
}
```

**Note:** The `property` array allows adding arbitrary properties as needed.

**Builder usage:**
```csharp
var device = new AiDeviceBuilder()
    .WithModelName("Trial Eligibility AI", "trail-match-ai")
    .WithManufacturer("ThirdOpinion.io AI Lab")
    .WithVersion("1.0.0")
    .Build();
```

### Example 2: ADT Therapy Detection Observation

```json
{
    "resourceType": "Observation",
    "id": "to.io-adt-status-obs-a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "identifier": [
        {
            "system": "https://thirdopinion.io/ai-inference",
            "value": "to.ai-inference-1"
        }
    ],
    "meta": {
        "security": [
            {
                "system": "http://terminology.hl7.org/CodeSystem/v3-ObservationValue",
                "code": "AIAST"
            }
        ]
    },
    "status": "final",
    "category": [
        {
            "coding": [
                {
                    "system": "http://terminology.hl7.org/CodeSystem/observation-category",
                    "code": "therapy",
                    "display": "Therapy"
                }
            ]
        }
    ],
    "code": {
        "coding": [
            {
                "system": "http://snomed.info/sct",
                "code": "413712001",
                "display": "Androgen deprivation therapy"
            }
        ],
        "text": "ADT Therapy Status"
    },
    "subject": {
        "reference": "Patient/example"
    },
    "effectiveDateTime": "2025-09-30T10:30:00Z",
    "performer": [
        {
            "reference": "Device/to.io-trial-eligibility-ai-v1"
        }
    ],
    "valueCodeableConcept": {
        "coding": [
            {
                "system": "http://snomed.info/sct",
                "code": "385654001",
                "display": "Active"
            }
        ],
        "text": "Receiving ADT"
    },
    "method": {
        "coding": [
            {
                "system": "https://thirdopinion.io/criteria",
                "code": "adt-therapy-1234455-v1.0",
                "display": "ThirdOpinion.io ADT Therapy(ID:1234455) v1.2.0"
            }
        ],
        "text": "ThirdOpinion.io on ADT therapy assessment"
    },
    "derivedFrom": [
        {
            "reference": "MedicationRequest/a-15454.med-192905",
            "display": "Eligard 20mg 2025-10-11"
        },
        {
            "reference": "DocumentReference/a-15454.doc-34567",
            "display": "Started Eligard Note Date: 2025-10-11"
        }
    ],
    "note": [
        {
            "text": "AI detected active ADT therapy based on: (1) Active MedicationStatement for leuprolide depot, (2) Current prescription dated 2024-06-15, (3) Clinical note from 2024-09-15 documenting patient compliance and tolerance. Confidence: 95%"
        }
    ]
}
```

**Builder usage:**
```csharp
var observation = new AdtStatusObservationBuilder()
    .WithInferenceId("to.ai-inference-1")
    .WithPatient(new ResourceReference("Patient/example"))
    .WithDevice(new ResourceReference("Device/to.io-trial-eligibility-ai-v1"))
    .WithCriteria("adt-therapy-1234455-v1.0", 
        "ThirdOpinion.io ADT Therapy(ID:1234455) v1.2.0",
        "ThirdOpinion.io on ADT therapy assessment")
    .WithStatus(true)
    .AddEvidence(new ResourceReference("MedicationRequest/a-15454.med-192905", "Eligard 20mg 2025-10-11"))
    .AddEvidence(new ResourceReference("DocumentReference/a-15454.doc-34567", "Started Eligard Note Date: 2025-10-11"))
    .WithEffectiveDate(new DateTime(2025, 9, 30, 10, 30, 0, DateTimeKind.Utc))
    .AddNote("AI detected active ADT therapy based on: (1) Active MedicationStatement for leuprolide depot, (2) Current prescription dated 2024-06-15, (3) Clinical note from 2024-09-15 documenting patient compliance and tolerance. Confidence: 95%")
    .Build();
```

### Example 3: CSPC Assessment Observation (UPDATED)

```json
{
    "resourceType": "Observation",
    "id": "to.io-cspc-assessment-001",
    "identifier": [
        {
            "system": "https://thirdopinion.io/ai-inference",
            "value": "to.ai-inference-1"
        }
    ],
    "meta": {
        "security": [
            {
                "system": "http://terminology.hl7.org/CodeSystem/v3-ObservationValue",
                "code": "AIAST"
            }
        ]
    },
    "status": "final",
    "category": [
        {
            "coding": [
                {
                    "system": "http://terminology.hl7.org/CodeSystem/observation-category",
                    "code": "exam",
                    "display": "Exam"
                }
            ]
        }
    ],
    "code": {
        "coding": [
            {
                "system": "http://loinc.org",
                "code": "21889-1",
                "display": "Cancer disease status"
            }
        ],
        "text": "Castration sensitivity status"
    },
    "subject": {
        "reference": "Patient/example"
    },
    "focus": [
        {
            "reference": "Condition/prostate-cancer-primary-001",
            "display": "Primary prostate cancer diagnosis (not AI-generated)"
        }
    ],
    "effectiveDateTime": "2025-09-30T10:30:00Z",
    "performer": [
        {
            "reference": "Device/to.io-trial-eligibility-ai-v2"
        }
    ],
    "valueCodeableConcept": {
        "coding": [
            {
                "system": "http://snomed.info/sct",
                "code": "1197209002",
                "display": "Castration-sensitive"
            },
            {
                "system": "http://hl7.org/fhir/sid/icd-10-cm",
                "code": "Z19.1",
                "display": "Hormone sensitive malignancy status"
            }
        ],
        "text": "Castration-Sensitive (CSPC)"
    },
    "interpretation": [
        {
            "coding": [
                {
                    "system": "http://terminology.hl7.org/CodeSystem/v3-ObservationInterpretation",
                    "code": "N",
                    "display": "Normal"
                }
            ],
            "text": "Disease responding to ADT, hormone-sensitive"
        }
    ],
    "derivedFrom": [
        {
            "reference": "Observation/testosterone-level-001",
            "display": "Testosterone 15 ng/dL (castrate level)"
        },
        {
            "reference": "Observation/psa-response-001",
            "display": "PSA decreased 80% on ADT"
        },
        {
            "reference": "Observation/to.io-adt-status-obs-001",
            "display": "Active ADT therapy"
        },
        {
            "reference": "DocumentReference/oncology-note-001"
        }
    ],
    "method": {
        "coding": [
            {
                "system": "https://thirdopinion.io/criteria",
                "code": "cspc-assessment-1234455-v1.0",
                "display": "ThirdOpinion.io CSPC Assessment(ID:1234455) v1.1"
            }
        ],
        "text": "ThirdOpinion.io AI inference for CSPC based on hormone levels, PSA kinetics, and treatment response"
    },
    "note": [
        {
            "text": "Classification derived from testosterone <50 ng/dL during ADT with PSA decline >50%, indicating castration sensitivity. No evidence of progression despite castrate testosterone levels."
        }
    ]
}
```

**Builder usage:**
```csharp
var assessment = new CspcAssessmentObservationBuilder()
    .WithInferenceId("to.ai-inference-1")
    .WithPatient(new ResourceReference("Patient/example"))
    .WithDevice(new ResourceReference("Device/to.io-trial-eligibility-ai-v2"))
    .WithFocus(new ResourceReference("Condition/prostate-cancer-primary-001", "Primary prostate cancer diagnosis (not AI-generated)"))
    .WithCriteria("cspc-assessment-1234455-v1.0",
        "ThirdOpinion.io CSPC Assessment(ID:1234455) v1.1",
        "ThirdOpinion.io AI inference for CSPC based on hormone levels, PSA kinetics, and treatment response")
    .WithCastrationSensitive(true)
    .AddEvidence(new ResourceReference("Observation/testosterone-level-001"), "Testosterone 15 ng/dL (castrate level)")
    .AddEvidence(new ResourceReference("Observation/psa-response-001"), "PSA decreased 80% on ADT")
    .AddEvidence(new ResourceReference("Observation/to.io-adt-status-obs-001"), "Active ADT therapy")
    .AddEvidence(new ResourceReference("DocumentReference/oncology-note-001"))
    .WithInterpretation("Disease responding to ADT, hormone-sensitive")
    .WithEffectiveDate(new DateTime(2025, 9, 30, 10, 30, 0, DateTimeKind.Utc))
    .AddNote("Classification derived from testosterone <50 ng/dL during ADT with PSA decline >50%, indicating castration sensitivity. No evidence of progression despite castrate testosterone levels.")
    .Build();
```

### Example 4: PSA Progression with ThirdOpinion.io Criteria

```json
{
    "resourceType": "Observation",
    "id": "to.io-psa-progression-001",
    "identifier": [
        {
            "system": "https://thirdopinion.io/ai-inference",
            "value": "to.ai-inference-1"
        }
    ],
    "meta": {
        "security": [
            {
                "system": "http://terminology.hl7.org/CodeSystem/v3-ObservationValue",
                "code": "AIAST"
            }
        ]
    },
    "status": "final",
    "category": [
        {
            "coding": [
                {
                    "system": "http://terminology.hl7.org/CodeSystem/observation-category",
                    "code": "laboratory",
                    "display": "Laboratory"
                }
            ]
        }
    ],
    "code": {
        "coding": [
            {
                "system": "http://loinc.org",
                "code": "97509-4",
                "display": "Cancer disease progression"
            }
        ],
        "text": "PSA progression assessment per ThirdOpinion.io criteria"
    },
    "subject": {
        "reference": "Patient/example"
    },
    "focus": [
        {
            "reference": "Condition/to.io-prostate-cancer-cspc-001"
        }
    ],
    "effectiveDateTime": "2025-09-30T10:30:00Z",
    "issued": "2025-09-30T10:35:00Z",
    "performer": [
        {
            "reference": "Device/to.io-trial-eligibility-ai-v2"
        }
    ],
    "valueCodeableConcept": {
        "coding": [
            {
                "system": "http://snomed.info/sct",
                "code": "277022003",
                "display": "Progressive disease"
            }
        ],
        "text": "PSA Progression per ThirdOpinion.io criteria"
    },
    "method": {
        "coding": [
            {
                "system": "https://thirdopinion.io/criteria",
                "code": "psa-progression-1234455-v1.2",
                "display": "ThirdOpinion.io PSA Progression Criteria ID:1234455 v1.2"
            }
        ],
        "text": "ThirdOpinion.io PSA progression criteria: ≥30% and ≥3 ng/mL increase from nadir, confirmed by second measurement ≥4 weeks later"
    },
    "derivedFrom": [
        {
            "reference": "Observation/psa-2024-07-15",
            "display": "PSA nadir 5.2 ng/mL on 2024-07-15"
        },
        {
            "reference": "Observation/psa-2024-09-01",
            "display": "PSA 9.8 ng/mL on 2024-09-01 (first rise)"
        },
        {
            "reference": "Observation/psa-2024-09-29",
            "display": "PSA 11.8 ng/mL on 2024-09-29 (confirmatory, 4 weeks later)"
        }
    ],
    "component": [
        {
            "code": {
                "text": "Valid until"
            },
            "valuePeriod": {
                "end": "2025-10-28"
            }
        },
        {
            "code": {
                "coding": [
                    {
                        "system": "https://thirdopinion.io/progression-criteria",
                        "code": "threshold-met",
                        "display": "Progression threshold met"
                    }
                ]
            },
            "valueBoolean": true
        }
    ],
    "note": [
        {
            "text": "PSA progression confirmed per ThirdOpinion.io criteria: ≥30% and ≥3 ng/mL increase from nadir (5.2→11.8 ng/mL, 127% increase), with sustained elevation above threshold on two consecutive measurements 4 weeks apart. This meets criteria for biochemical progression."
        },
        {
            "text": "PSA Analysis - Prostate Cancer Treatment Response\nChronological PSA Values: 11/14/24: Total PSA = 85.23 ng/mL (SEVERELY ELEVATED - Reference: 0.00-4.00)\n11/14/24: Free PSA = 4.8 ng/mL (HIGH - Reference: <0.94)\n11/14/24: PSA Ratio = 0.06 (Low - concerning for malignancy)\n1/23/25: Prostate Biopsy Performed (observation_id: a-15454.resultamb-7520175)\n3/6/25: Total PSA = 96.46 ng/mL (PEAK ELEVATION - continued progression)\n4/30/25: CONFLICTING VALUES:\n- PSA = 17.97 ng/mL (3:53 PM) - observation_id: a-15454.resultamb-7893359\n- PSA = 0.38 ng/mL (3:55 PM) - observation_id: a-15454.resultamb-7893360\n7/24/25: Total PSA = 0.1 ng/mL (EXCELLENT RESPONSE - within normal range)\n\nPSA Progression Analysis\nInitial Disease Status:\n- PSA Peak: 96.46 ng/mL (March 6, 2025)\n- Clinical Pattern: Extremely aggressive prostate cancer with very high PSA levels\n- Biopsy Confirmation: January 23, 2025\n\nTreatment Response Analysis:\n- Treatment Initiation: Likely began between March-April 2025\n- Response Quality: EXCELLENT - PSA decreased from 96.46 to 0.1 ng/mL (>99% reduction)\n- Current Status: PSA well within normal range\n\nData Quality Concern:\n⚠️ CRITICAL DATA ISSUE: Two conflicting PSA values on 4/30/25 (17.97 vs 0.38 ng/mL) taken 2 minutes apart require investigation for accuracy."
        }
    ]
}
```

**Builder usage:**
```csharp
var progression = new PsaProgressionObservationBuilder()
    .WithInferenceId("to.ai-inference-1")
    .WithPatient(new ResourceReference("Patient/example"))
    .WithDevice(new ResourceReference("Device/to.io-trial-eligibility-ai-v2"))
    .WithFocus(new ResourceReference("Condition/to.io-prostate-cancer-cspc-001"))
    .WithCriteria("psa-progression-1234455-v1.2",
        "ThirdOpinion.io PSA Progression Criteria ID:1234455 v1.2",
        "ThirdOpinion.io PSA progression criteria: ≥30% and ≥3 ng/mL increase from nadir, confirmed by second measurement ≥4 weeks later")
    .AddPsaEvidence(new ResourceReference("Observation/psa-2024-07-15"), "PSA nadir 5.2 ng/mL on 2024-07-15")
    .AddPsaEvidence(new ResourceReference("Observation/psa-2024-09-01"), "PSA 9.8 ng/mL on 2024-09-01 (first rise)")
    .AddPsaEvidence(new ResourceReference("Observation/psa-2024-09-29"), "PSA 11.8 ng/mL on 2024-09-29 (confirmatory, 4 weeks later)")
    .WithProgression(true)
    .AddValidUntilComponent(new DateTime(2025, 10, 28))
    .AddThresholdMetComponent(true)
    .AddNote("PSA progression confirmed per ThirdOpinion.io criteria: ≥30% and ≥3 ng/mL increase from nadir (5.2→11.8 ng/mL, 127% increase)")
    .AddDetailedAnalysisNote("PSA Analysis - Prostate Cancer Treatment Response\nChronological PSA Values: 11/14/24: Total PSA = 85.23 ng/mL (SEVERELY ELEVATED)...")
    .Build();
```

### Example 5: PSA Progression with PCWG3 Criteria

Use the same structure as Example 4, but replace the `method.coding` and criteria-specific values:

```json
"method": {
    "coding": [
        {
            "system": "https://thirdopinion.io/criteria",
            "code": "psa-progression-pcwg3-1234455-v1.0",
            "display": "ThirdOpinion.io PCWG3 PSA Progression Criteria ID:1234455 v1.0"
        }
    ],
    "text": "PCWG3 (Prostate Cancer Working Group 3) PSA progression criteria: ≥25% and ≥2 ng/mL increase from nadir"
}
```

The rest of the resource structure follows Example 4.

### Example 6: RECIST 1.1 Radiographic Progression

```json
{
    "resourceType": "Observation",
    "id": "to.io-recist-progression-f6a7b8c9-d0e1-2345-f012-3456789cdef0",
    "identifier": [
        {
            "system": "https://thirdopinion.io/ai-inference",
            "value": "to.ai-inference-1"
        }
    ],
    "meta": {
        "security": [
            {
                "system": "http://terminology.hl7.org/CodeSystem/v3-ObservationValue",
                "code": "AIAST"
            }
        ]
    },
    "status": "final",
    "category": [
        {
            "coding": [
                {
                    "system": "http://terminology.hl7.org/CodeSystem/observation-category",
                    "code": "imaging",
                    "display": "Imaging"
                }
            ]
        }
    ],
    "code": {
        "coding": [
            {
                "system": "http://loinc.org",
                "code": "21976-6",
                "display": "Tumor response"
            },
            {
                "system": "http://ncicb.nci.nih.gov/xml/owl/EVS/Thesaurus.owl",
                "code": "C111544",
                "display": "RECIST 1.1"
            }
        ],
        "text": "RECIST 1.1 Response Assessment"
    },
    "subject": {
        "reference": "Patient/example"
    },
    "focus": [
        {
            "reference": "Condition/to.io-prostate-cancer-cspc-001"
        }
    ],
    "effectiveDateTime": "2025-09-25T10:00:00Z",
    "issued": "2025-09-26T09:15:00Z",
    "performer": [
        {
            "reference": "Device/to.io-trial-eligibility-ai-v2"
        }
    ],
    "valueCodeableConcept": {
        "coding": [
            {
                "system": "http://ncicb.nci.nih.gov/xml/owl/EVS/Thesaurus.owl",
                "code": "C35571",
                "display": "Progressive Disease"
            }
        ],
        "text": "Progressive Disease (PD) per RECIST 1.1"
    },
    "method": {
        "coding": [
            {
                "system": "https://thirdopinion.io/criteria",
                "code": "radiology-progression-1234455-v1.0",
                "display": "ThirdOpinion.io RECIST 1.1 Progression Criteria ID:1234455 v1.0"
            }
        ],
        "text": "ThirdOpinion.io AI Response Evaluation Criteria in Solid Tumors version 1.1"
    },
    "bodySite": [
        {
            "coding": [
                {
                    "system": "http://snomed.info/sct",
                    "code": "10200004",
                    "display": "Liver structure"
                }
            ]
        },
        {
            "coding": [
                {
                    "system": "http://snomed.info/sct",
                    "code": "39607008",
                    "display": "Lung structure"
                }
            ]
        }
    ],
    "derivedFrom": [
        {
            "reference": "ImagingStudy/ct-chest-abdomen-2025-09-25",
            "display": "CT Chest/Abdomen with contrast 2025-09-25"
        },
        {
            "reference": "Observation/to.io-recist-baseline-2024-06-01",
            "display": "RECIST baseline assessment"
        },
        {
            "reference": "Observation/to.io-recist-nadir-2024-08-15",
            "display": "RECIST nadir assessment (SLD 62mm)"
        },
        {
            "reference": "DocumentReference/radiology-report-2025-09-25",
            "display": "Radiology report by Dr. Smith"
        }
    ],
    "component": [
        {
            "code": {
                "text": "Sum of target lesion diameters (SLD)"
            },
            "valueQuantity": {
                "value": 78,
                "unit": "mm",
                "system": "http://unitsofmeasure.org",
                "code": "mm"
            }
        },
        {
            "code": {
                "text": "SLD at nadir"
            },
            "valueQuantity": {
                "value": 62,
                "unit": "mm",
                "system": "http://unitsofmeasure.org",
                "code": "mm"
            }
        },
        {
            "code": {
                "text": "Percent change from nadir"
            },
            "valueQuantity": {
                "value": 25.8,
                "unit": "%",
                "system": "http://unitsofmeasure.org",
                "code": "%"
            }
        },
        {
            "code": {
                "text": "Absolute change from nadir"
            },
            "valueQuantity": {
                "value": 16,
                "unit": "mm",
                "system": "http://unitsofmeasure.org",
                "code": "mm"
            }
        },
        {
            "code": {
                "text": "New lesions detected"
            },
            "valueBoolean": true
        },
        {
            "code": {
                "text": "New lesion location"
            },
            "valueCodeableConcept": {
                "coding": [
                    {
                        "system": "http://snomed.info/sct",
                        "code": "10200004",
                        "display": "Liver structure"
                    }
                ]
            }
        }
    ],
    "note": [
        {
            "text": "Progressive disease based on >20% increase in sum of target lesion diameters (SLD) from nadir (62→78mm, 25.8% increase, absolute increase 16mm >5mm threshold). New hepatic lesion also identified."
        },
        {
            "text": "PSA Analysis - Prostate Cancer Treatment Response\nChronological PSA Values: 11/14/24: Total PSA = 85.23 ng/mL (SEVERELY ELEVATED - Reference: 0.00-4.00)\n11/14/24: Free PSA = 4.8 ng/mL (HIGH - Reference: <0.94)"
        }
    ]
}
```

**Builder usage:**
```csharp
var recist = new RecistProgressionObservationBuilder()
    .WithInferenceId("to.ai-inference-1")
    .WithPatient(new ResourceReference("Patient/example"))
    .WithDevice(new ResourceReference("Device/to.io-trial-eligibility-ai-v2"))
    .WithFocus(new ResourceReference("Condition/to.io-prostate-cancer-cspc-001"))
    .WithCriteria("radiology-progression-1234455-v1.0",
        "ThirdOpinion.io RECIST 1.1 Progression Criteria ID:1234455 v1.0",
        "ThirdOpinion.io AI Response Evaluation Criteria in Solid Tumors version 1.1")
    .AddImagingStudy(new ResourceReference("ImagingStudy/ct-chest-abdomen-2025-09-25"), "CT Chest/Abdomen with contrast 2025-09-25")
    .AddRadiologyReport(new ResourceReference("DocumentReference/radiology-report-2025-09-25"), "Radiology report by Dr. Smith")
    .WithRecistResponse("C35571", "Progressive Disease")
    .AddBodySite("10200004", "Liver structure")
    .AddBodySite("39607008", "Lung structure")
    .AddComponent("Sum of target lesion diameters (SLD)", new Quantity { Value = 78, Unit = "mm", System = "http://unitsofmeasure.org", Code = "mm" })
    .AddComponent("New lesions detected", true)
    .AddNote("Progressive disease based on >20% increase in SLD from nadir (62→78mm, 25.8% increase)")
    .Build();
```

### Example 7: Provenance Resource for Audit Trail

**With optional log file S3 reference:**
```json
{
    "resourceType": "Provenance",
    "id": "to.io-prov-psa-progression-f6a7b8c9-d0e1-2345-f012-3456789cdef0",
    "identifier": [
        {
            "system": "https://thirdopinion.io/provenance-tracking",
            "value": "to.io-prov-1"
        }
    ],
    "target": [
        {
            "reference": "Observation/to.io-psa-progression-001/_history/1"
        }
    ],
    "occurredDateTime": "2025-09-30T10:30:00Z",
    "recorded": "2025-09-30T10:30:15Z",
    "policy": [
        "https://thirdopinion.io/policies/ai-clinical-use-v1"
    ],
    "reason": [
        {
            "coding": [
                {
                    "system": "http://terminology.hl7.org/CodeSystem/v3-ActReason",
                    "code": "TREAT",
                    "display": "treatment"
                }
            ],
            "text": "Clinical trial eligibility assessment"
        }
    ],
    "activity": {
        "coding": [
            {
                "system": "http://terminology.hl7.org/CodeSystem/v3-DataOperation",
                "code": "CREATE",
                "display": "create"
            }
        ]
    },
    "agent": [
        {
            "type": {
                "coding": [
                    {
                        "system": "http://terminology.hl7.org/CodeSystem/provenance-participant-type",
                        "code": "assembler",
                        "display": "Assembler"
                    }
                ]
            },
            "who": {
                "reference": "Device/to.io-trial-eligibility-ai-v1",
                "display": "Prostate Cancer Trial Eligibility AI v1.0"
            }
        },
        {
            "type": {
                "coding": [
                    {
                        "system": "http://terminology.hl7.org/CodeSystem/provenance-participant-type",
                        "code": "author",
                        "display": "Author"
                    }
                ]
            },
            "who": {
                "reference": "Organization/thirdopinion-ai-lab"
            }
        }
    ],
    "entity": [
        {
            "role": "source",
            "what": {
                "reference": "Observation/psa-2024-07-15",
                "display": "PSA nadir 5.2 ng/mL"
            }
        },
        {
            "role": "source",
            "what": {
                "reference": "Observation/psa-2024-09-01",
                "display": "PSA 9.8 ng/mL"
            }
        },
        {
            "role": "source",
            "what": {
                "reference": "Observation/psa-2024-09-29",
                "display": "PSA 11.8 ng/mL (confirmatory)"
            }
        },
        {
            "role": "derivation",
            "what": {
                "reference": "DocumentReference/to.io-inference-log-001",
                "display": "AI inference execution log"
            }
        }
    ]
}
```

**Associated DocumentReference for log file:**
```json
{
    "resourceType": "DocumentReference",
    "id": "to.io-inference-log-001",
    "identifier": [
        {
            "system": "https://thirdopinion.io/ai-inference",
            "value": "to.ai-inference-log-1"
        }
    ],
    "status": "current",
    "docStatus": "final",
    "type": {
        "coding": [
            {
                "system": "http://loinc.org",
                "code": "11502-2",
                "display": "Laboratory report"
            }
        ]
    },
    "category": [
        {
            "coding": [
                {
                    "system": "https://thirdopinion.io/document-categories",
                    "code": "ai-log",
                    "display": "AI Inference Log"
                }
            ]
        }
    ],
    "subject": {
        "reference": "Patient/example"
    },
    "date": "2025-09-30T10:30:15Z",
    "author": [
        {
            "reference": "Device/to.io-trial-eligibility-ai-v1"
        }
    ],
    "description": "Detailed AI inference execution log with model parameters, input checksums, and decision trace",
    "content": [
        {
            "attachment": {
                "contentType": "application/json",
                "url": "s3://thirdopinion-logs/inference/2025/09/30/to.ai-inference-1-execution.log",
                "title": "AI Inference Execution Log",
                "creation": "2025-09-30T10:30:15Z"
            }
        }
    ]
}
```

**Builder usage:**
```csharp
var provenance = new AiProvenanceBuilder()
    .WithProvenanceId("to.io-prov-1")
    .ForTarget(new ResourceReference("Observation/to.io-psa-progression-001/_history/1"))
    .WithOccurredDateTime(new DateTime(2025, 9, 30, 10, 30, 0, DateTimeKind.Utc))
    .WithAiDevice(new ResourceReference("Device/to.io-trial-eligibility-ai-v1"), "Prostate Cancer Trial Eligibility AI v1.0")
    .WithOrganization(new ResourceReference("Organization/thirdopinion-ai-lab"))
    .WithPolicy("https://thirdopinion.io/policies/ai-clinical-use-v1")
    .WithReason("TREAT", "Clinical trial eligibility assessment")
    .AddSourceEntity(new ResourceReference("Observation/psa-2024-07-15"), "PSA nadir 5.2 ng/mL")
    .AddSourceEntity(new ResourceReference("Observation/psa-2024-09-01"), "PSA 9.8 ng/mL")
    .AddSourceEntity(new ResourceReference("Observation/psa-2024-09-29"), "PSA 11.8 ng/mL (confirmatory)")
    .WithLogFileUrl("s3://thirdopinion-logs/inference/2025/09/30/to.ai-inference-1-execution.log")
    .Build();
```

**Log file contents example:**
The S3 log file typically contains:
```json
{
  "inferenceId": "to.ai-inference-1",
  "timestamp": "2025-09-30T10:30:00Z",
  "modelVersion": "trial-eligibility-v1.0",
  "modelParameters": {
    "temperature": 0.0,
    "maxTokens": 2048,
    "criteriaVersion": "psa-progression-v1.2"
  },
  "inputChecksums": {
    "psa-2024-07-15": "sha256:a3b2c1...",
    "psa-2024-09-01": "sha256:b4c3d2...",
    "psa-2024-09-29": "sha256:c5d4e3..."
  },
  "decisionTrace": {
    "nadirPsa": 5.2,
    "currentPsa": 11.8,
    "percentIncrease": 127,
    "absoluteIncrease": 6.6,
    "thresholdMet": true,
    "confidenceScore": 0.98
  },
  "executionTime": "1.23s",
  "resourceUsage": {
    "cpuMs": 1230,
    "memoryMb": 512
  }
}
```

### Example 8: Original Scanned Document

```json
{
    "resourceType": "DocumentReference",
    "id": "to.io-radiology-report-scan-001",
    "status": "current",
    "docStatus": "final",
    "type": {
        "coding": [
            {
                "system": "http://loinc.org",
                "code": "18748-4",
                "display": "Diagnostic imaging study"
            }
        ]
    },
    "category": [
        {
            "coding": [
                {
                    "system": "http://hl7.org/fhir/us/core/CodeSystem/us-core-documentreference-category",
                    "code": "clinical-note"
                }
            ]
        }
    ],
    "subject": {
        "reference": "Patient/example"
    },
    "date": "2025-09-25T14:00:00Z",
    "author": [
        {
            "reference": "Practitioner/radiologist-smith"
        }
    ],
    "content": [
        {
            "attachment": {
                "contentType": "application/pdf",
                "url": "Binary/radiology-scan-001",
                "title": "CT Chest/Abdomen Radiology Report",
                "creation": "2025-09-25T14:00:00Z",
                "size": 1245760,
                "hash": "2jmj7l5rSw0yVb/vlWAYkK/YBwk="
            },
            "format": {
                "system": "http://ihe.net/fhir/ihe.formatcode.fhir/CodeSystem/formatcode",
                "code": "urn:ihe:iti:xds:2016:pdf",
                "display": "PDF"
            }
        }
    ]
}
```

**Note:** This is not an AI-generated resource, so it does not have the AIAST security label or AI-specific identifiers.

### Example 9: OCR Text DocumentReference

**Option A: Inline text content**
```json
{
    "resourceType": "DocumentReference",
    "id": "to.io-radiology-report-ocr-001",
    "identifier": [
        {
            "system": "https://thirdopinion.io/ai-inference",
            "value": "to.ai-inference-doc-1"
        }
    ],
    "meta": {
        "security": [
            {
                "system": "http://terminology.hl7.org/CodeSystem/v3-ObservationValue",
                "code": "AIAST"
            }
        ]
    },
    "status": "current",
    "docStatus": "final",
    "type": {
        "coding": [
            {
                "system": "http://loinc.org",
                "code": "18748-4",
                "display": "Diagnostic imaging study"
            }
        ]
    },
    "category": [
        {
            "coding": [
                {
                    "system": "http://hl7.org/fhir/us/core/CodeSystem/us-core-documentreference-category",
                    "code": "clinical-note"
                }
            ]
        }
    ],
    "subject": {
        "reference": "Patient/example"
    },
    "date": "2025-09-26T08:00:00Z",
    "author": [
        {
            "reference": "Device/to.io-ocr-engine-v3",
            "display": "ThirdOpinion.io OCR Processing Engine v3.1"
        }
    ],
    "description": "OCR-extracted text from radiology report scan",
    "relatesTo": [
        {
            "code": "transforms",
            "target": {
                "reference": "DocumentReference/to.io-radiology-report-scan-001"
            }
        }
    ],
    "content": [
        {
            "attachment": {
                "contentType": "text/plain;charset=utf-8",
                "data": "Q1QgQ0hFU1QvQUJET01FTiB3aXRoIENvbnRyYXN0Li4uRklORElOR1M6IE5ldyBoeXBvZGVuc2UgbGVzaW9uIGluIHJpZ2h0IGhlcGF0aWMgbG9iZS4uLg==",
                "title": "OCR Text - Radiology Report",
                "creation": "2025-09-26T08:00:00Z",
                "language": "en-US"
            },
            "format": {
                "system": "urn:ietf:bcp:13",
                "code": "text/plain",
                "display": "Plain Text"
            }
        }
    ]
}
```

**Option B: S3 URL references with Textract outputs**
```json
{
    "resourceType": "DocumentReference",
    "id": "to.io-radiology-report-ocr-002",
    "identifier": [
        {
            "system": "https://thirdopinion.io/ai-inference",
            "value": "to.ai-inference-doc-2"
        }
    ],
    "meta": {
        "security": [
            {
                "system": "http://terminology.hl7.org/CodeSystem/v3-ObservationValue",
                "code": "AIAST"
            }
        ]
    },
    "status": "current",
    "docStatus": "final",
    "type": {
        "coding": [
            {
                "system": "http://loinc.org",
                "code": "18748-4",
                "display": "Diagnostic imaging study"
            }
        ]
    },
    "category": [
        {
            "coding": [
                {
                    "system": "http://hl7.org/fhir/us/core/CodeSystem/us-core-documentreference-category",
                    "code": "clinical-note"
                }
            ]
        }
    ],
    "subject": {
        "reference": "Patient/example"
    },
    "date": "2025-09-26T08:00:00Z",
    "author": [
        {
            "reference": "Device/to.io-textract-engine-v1",
            "display": "AWS Textract OCR Engine"
        }
    ],
    "description": "OCR outputs stored in S3 with Textract processing",
    "relatesTo": [
        {
            "code": "transforms",
            "target": {
                "reference": "DocumentReference/to.io-radiology-report-scan-001"
            }
        }
    ],
    "content": [
        {
            "attachment": {
                "contentType": "text/plain;charset=utf-8",
                "url": "s3://thirdopinion-ocr/patient-123/reports/radiology-2025-09-26-text.txt",
                "title": "Extracted Text",
                "creation": "2025-09-26T08:00:00Z"
            },
            "format": {
                "system": "urn:ietf:bcp:13",
                "code": "text/plain",
                "display": "Plain Text"
            }
        },
        {
            "attachment": {
                "contentType": "application/json",
                "url": "s3://thirdopinion-ocr/patient-123/reports/radiology-2025-09-26-textract-raw.json",
                "title": "AWS Textract Raw Output",
                "creation": "2025-09-26T08:00:00Z"
            },
            "format": {
                "system": "urn:ietf:bcp:13",
                "code": "application/json",
                "display": "JSON Format"
            }
        },
        {
            "attachment": {
                "contentType": "application/json",
                "url": "s3://thirdopinion-ocr/patient-123/reports/radiology-2025-09-26-textract-simple.json",
                "title": "AWS Textract Simplified Output",
                "creation": "2025-09-26T08:00:00Z"
            },
            "format": {
                "system": "urn:ietf:bcp:13",
                "code": "application/json",
                "display": "JSON Format"
            }
        }
    ]
}
```

**Builder usage - Option A (inline text):**
```csharp
var ocrDoc = new OcrDocumentReferenceBuilder()
    .WithInferenceId("to.ai-inference-doc-1")
    .WithOriginalDocument(new ResourceReference("DocumentReference/to.io-radiology-report-scan-001"))
    .WithPatient(new ResourceReference("Patient/example"))
    .WithOcrDevice(new ResourceReference("Device/to.io-ocr-engine-v3", "ThirdOpinion.io OCR Processing Engine v3.1"))
    .WithExtractedText("CT CHEST/ABDOMEN with Contrast...FINDINGS: New hypodense lesion in right hepatic lobe...")
    .WithDescription("OCR-extracted text from radiology report scan")
    .Build();
```

**Builder usage - Option B (S3 URLs with Textract):**
```csharp
var ocrDoc = new OcrDocumentReferenceBuilder()
    .WithInferenceId("to.ai-inference-doc-2")
    .WithOriginalDocument(new ResourceReference("DocumentReference/to.io-radiology-report-scan-001"))
    .WithPatient(new ResourceReference("Patient/example"))
    .WithOcrDevice(new ResourceReference("Device/to.io-textract-engine-v1", "AWS Textract OCR Engine"))
    .WithExtractedTextUrl("s3://thirdopinion-ocr/patient-123/reports/radiology-2025-09-26-text.txt")
    .WithTextractRawUrl("s3://thirdopinion-ocr/patient-123/reports/radiology-2025-09-26-textract-raw.json")
    .WithTextractSimpleUrl("s3://thirdopinion-ocr/patient-123/reports/radiology-2025-09-26-textract-simple.json")
    .WithDescription("OCR outputs stored in S3 with Textract processing")
    .Build();
```

### Example 10: Fact Extraction JSON DocumentReference

**Option A: Inline JSON content**
```json
{
    "resourceType": "DocumentReference",
    "id": "to.io-radiology-facts-001",
    "identifier": [
        {
            "system": "https://thirdopinion.io/ai-inference",
            "value": "to.ai-inference-facts-1"
        }
    ],
    "meta": {
        "security": [
            {
                "system": "http://terminology.hl7.org/CodeSystem/v3-ObservationValue",
                "code": "AIAST"
            }
        ]
    },
    "status": "current",
    "docStatus": "final",
    "type": {
        "coding": [
            {
                "system": "http://loinc.org",
                "code": "18748-4",
                "display": "Diagnostic imaging study"
            }
        ]
    },
    "category": [
        {
            "coding": [
                {
                    "system": "https://thirdopinion.io/document-categories",
                    "code": "extracted-facts",
                    "display": "AI-Extracted Clinical Facts"
                }
            ]
        }
    ],
    "subject": {
        "reference": "Patient/example"
    },
    "date": "2025-09-26T08:05:00Z",
    "author": [
        {
            "reference": "Device/to.io-clinical-nlp-extraction-v3",
            "display": "ThirdOpinion.io Clinical NLP Fact Extraction v3.0"
        }
    ],
    "description": "Structured clinical facts extracted from radiology report via NLP",
    "relatesTo": [
        {
            "code": "transforms",
            "target": {
                "reference": "DocumentReference/to.io-radiology-report-ocr-001"
            }
        },
        {
            "code": "transforms",
            "target": {
                "reference": "DocumentReference/to.io-radiology-report-scan-001"
            }
        }
    ],
    "content": [
        {
            "attachment": {
                "contentType": "application/json",
                "data": "ewogICJmaW5kaW5ncyI6IFsKICAgIHsKICAgICAgImNvbmNlcHQiOiAiTmV3IGxpdmVyIG1ldGFzdGFzaXMiLAogICAgICAic25vbWVkIjogIjk0MjIyMDA4IiwKICAgICAgImxvY2F0aW9uIjogIlJpZ2h0IGhlcGF0aWMgbG9iZSIsCiAgICAgICJzaXplIjogIjEuMiBjbSIsCiAgICAgICJjb25maWRlbmNlIjogMC45MgogICAgfSwKICAgIHsKICAgICAgImNvbmNlcHQiOiAiUHJvZ3Jlc3NpdmUgZGlzZWFzZSIsCiAgICAgICJzbm9tZWQiOiAiMjc3MDIyMDAzIiwKICAgICAgImV2aWRlbmNlIjogIk5ldyBsZXNpb24gaWRlbnRpZmllZCIsCiAgICAgICJjb25maWRlbmNlIjogMC45NAogICAgfQogIF0sCiAgInByb3RvY29sIjogIlJFQ0lTVCAxLjEiLAogICAgIm1vZGVsVmVyc2lvbiI6ICJjbGluaWNhbC1ubHAtdjMuMC4xIgp9",
                "title": "Extracted Clinical Facts JSON",
                "creation": "2025-09-26T08:05:00Z"
            },
            "format": {
                "system": "urn:ietf:bcp:13",
                "code": "application/json",
                "display": "JSON Format"
            }
        }
    ]
}
```

**Option B: S3 URL reference**
```json
{
    "resourceType": "DocumentReference",
    "id": "to.io-radiology-facts-002",
    "identifier": [
        {
            "system": "https://thirdopinion.io/ai-inference",
            "value": "to.ai-inference-facts-2"
        }
    ],
    "meta": {
        "security": [
            {
                "system": "http://terminology.hl7.org/CodeSystem/v3-ObservationValue",
                "code": "AIAST"
            }
        ]
    },
    "status": "current",
    "docStatus": "final",
    "type": {
        "coding": [
            {
                "system": "http://loinc.org",
                "code": "18748-4",
                "display": "Diagnostic imaging study"
            }
        ]
    },
    "category": [
        {
            "coding": [
                {
                    "system": "https://thirdopinion.io/document-categories",
                    "code": "extracted-facts",
                    "display": "AI-Extracted Clinical Facts"
                }
            ]
        }
    ],
    "subject": {
        "reference": "Patient/example"
    },
    "date": "2025-09-26T08:05:00Z",
    "author": [
        {
            "reference": "Device/to.io-clinical-nlp-extraction-v3",
            "display": "ThirdOpinion.io Clinical NLP Fact Extraction v3.0"
        }
    ],
    "description": "Structured clinical facts stored in S3",
    "relatesTo": [
        {
            "code": "transforms",
            "target": {
                "reference": "DocumentReference/to.io-radiology-report-ocr-002"
            }
        },
        {
            "code": "transforms",
            "target": {
                "reference": "DocumentReference/to.io-radiology-report-scan-001"
            }
        }
    ],
    "content": [
        {
            "attachment": {
                "contentType": "application/json",
                "url": "s3://thirdopinion-facts/patient-123/reports/radiology-2025-09-26-facts.json",
                "title": "Extracted Clinical Facts JSON",
                "creation": "2025-09-26T08:05:00Z"
            },
            "format": {
                "system": "urn:ietf:bcp:13",
                "code": "application/json",
                "display": "JSON Format"
            }
        }
    ]
}
```

**Decoded JSON content (Option A):**
```json
{
  "findings": [
    {
      "concept": "New liver metastasis",
      "snomed": "94222008",
      "location": "Right hepatic lobe",
      "size": "1.2 cm",
      "confidence": 0.92
    },
    {
      "concept": "Progressive disease",
      "snomed": "277022003",
      "evidence": "New lesion identified",
      "confidence": 0.94
    }
  ],
  "protocol": "RECIST 1.1",
  "modelVersion": "clinical-nlp-v3.0.1"
}
```

**Builder usage - Option A (inline JSON):**
```csharp
var factsObject = new
{
    findings = new[]
    {
        new
        {
            concept = "New liver metastasis",
            snomed = "94222008",
            location = "Right hepatic lobe",
            size = "1.2 cm",
            confidence = 0.92
        },
        new
        {
            concept = "Progressive disease",
            snomed = "277022003",
            evidence = "New lesion identified",
            confidence = 0.94
        }
    },
    protocol = "RECIST 1.1",
    modelVersion = "clinical-nlp-v3.0.1"
};

var factsDoc = new FactExtractionDocumentReferenceBuilder()
    .WithInferenceId("to.ai-inference-facts-1")
    .WithOriginalDocument(new ResourceReference("DocumentReference/to.io-radiology-report-scan-001"))
    .WithOcrDocument(new ResourceReference("DocumentReference/to.io-radiology-report-ocr-001"))
    .WithPatient(new ResourceReference("Patient/example"))
    .WithExtractionDevice(new ResourceReference("Device/to.io-clinical-nlp-extraction-v3", "ThirdOpinion.io Clinical NLP Fact Extraction v3.0"))
    .WithFactsJson(factsObject)  // Auto-serializes to JSON and Base64-encodes
    .WithDescription("Structured clinical facts extracted from radiology report via NLP")
    .Build();
```

**Builder usage - Option B (S3 URL):**
```csharp
var factsDoc = new FactExtractionDocumentReferenceBuilder()
    .WithInferenceId("to.ai-inference-facts-2")
    .WithOriginalDocument(new ResourceReference("DocumentReference/to.io-radiology-report-scan-001"))
    .WithOcrDocument(new ResourceReference("DocumentReference/to.io-radiology-report-ocr-002"))
    .WithPatient(new ResourceReference("Patient/example"))
    .WithExtractionDevice(new ResourceReference("Device/to.io-clinical-nlp-extraction-v3", "ThirdOpinion.io Clinical NLP Fact Extraction v3.0"))
    .WithFactsJsonUrl("s3://thirdopinion-facts/patient-123/reports/radiology-2025-09-26-facts.json")
    .WithDescription("Structured clinical facts stored in S3")
    .Build();
```

### Document Processing Architecture

The document processing workflow creates a chain of DocumentReference resources:

```
Original Scanned Document (DocumentReference A)
    ↓ OCR processing
OCR Text Version (DocumentReference B)
    → relatesTo: { code: "transforms", target: DocumentReference/A }
    ↓ Fact extraction
Structured Facts JSON (DocumentReference C)
    → relatesTo: { code: "transforms", target: DocumentReference/B }
    → relatesTo: { code: "transforms", target: DocumentReference/A }
```

**Key points:**
- Use `relatesTo` with code "transforms" to link derived documents to originals
- AI-generated documents get AIAST security label
- Use simplified identifier system: `https://thirdopinion.io/ai-inference`
- Base64-encode content in the `attachment.data` field
- OCR and extraction devices are represented as Device resources with appropriate references

## Additional Notes

### Analogies for Design Patterns

**Builder Pattern = LINQ Query Composition**
```csharp
// Like building a LINQ query
var query = patients
    .Where(p => p.HasProstateCancer)
    .Select(p => p.Name)
    .OrderBy(n => n);

// Building a FHIR resource
var obs = new AdtStatusObservationBuilder()
    .WithPatient(patientRef)
    .WithStatus(true)
    .AddEvidence(medRef)
    .Build();
```

**derivedFrom = SQL JOIN**
Think of derivedFrom references like SQL joins - they link the AI inference back to the source data rows (other FHIR resources) that were "joined" to create this result.

**Provenance = Git Commit Metadata**
Just like Git tracks who made a change, when, and what files were involved, Provenance tracks which AI system created a resource, when, and what source data it used.

**focus Field = Foreign Key to Primary Diagnosis**
The focus field is like a foreign key in a database - it points to the "parent record" (existing Condition) that this assessment is about.