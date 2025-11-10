# Observation Builders

FHIR Observation resource builders for AI-generated clinical assessments in prostate cancer care.

## Overview

These builders create FHIR Observation resources that represent AI-generated clinical inferences. Each builder
specializes in a specific type of clinical assessment while maintaining consistency through the shared base class
architecture.

## Available Builders

### AdtStatusObservationBuilder

Creates observations for Androgen Deprivation Therapy (ADT) status detection.

#### Purpose

Generates FHIR Observations that indicate whether a patient is currently receiving ADT therapy based on AI analysis of
clinical data.

#### Key Features

- **Category**: `exam` (Clinical examination)
- **Code**: LOINC `LA4633-1` (Treatment status)
- **Value**: Boolean indicating ADT status
- **Evidence**: References to medication statements, prescriptions, and clinical notes

#### Basic Usage

```csharp
var config = AiInferenceConfiguration.CreateDefault();

var adtObservation = new AdtStatusObservationBuilder(config)
    .WithInferenceId("adt-assessment-001")
    .WithPatient("Patient/prostate-patient-123", "John Smith")
    .WithDevice("Device/adt-detection-ai-v2", "ADT Detection AI v2.1")
    .WithStatus(true, confidenceScore: 0.94f) // Patient is on ADT with 94% confidence
    .AddEvidence("MedicationStatement/lupron-injection", "Active Lupron injection")
    .AddEvidence("MedicationRequest/bicalutamide", "Bicalutamide prescription")
    .WithEffectiveDate(DateTime.UtcNow)
    .AddNote("Patient shows evidence of active ADT based on medication analysis")
    .Build();
```

#### Example Output

```json
{
  "resourceType": "Observation",
  "id": "adt-assessment-001",
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
        "code": "LA4633-1",
        "display": "Treatment status"
      }
    ],
    "text": "ADT therapy status"
  },
  "subject": {
    "reference": "Patient/prostate-patient-123",
    "display": "John Smith"
  },
  "valueBoolean": true,
  "component": [
    {
      "code": {
        "coding": [
          {
            "system": "http://loinc.org",
            "code": "LA11892-6",
            "display": "Probability"
          }
        ],
        "text": "AI Confidence Score"
      },
      "valueQuantity": {
        "value": 0.94,
        "unit": "probability",
        "system": "http://unitsofmeasure.org",
        "code": "1"
      }
    }
  ]
}
```

#### API Reference

```csharp
public AdtStatusObservationBuilder WithInferenceId(string id)
public AdtStatusObservationBuilder WithPatient(ResourceReference patientRef)
public AdtStatusObservationBuilder WithPatient(string patientId, string? display = null)
public AdtStatusObservationBuilder WithDevice(ResourceReference deviceRef)
public AdtStatusObservationBuilder WithDevice(string deviceId, string? display = null)
public AdtStatusObservationBuilder WithStatus(bool isOnAdt, float? confidence = null)
public AdtStatusObservationBuilder AddEvidence(ResourceReference reference, string? displayText = null)
public AdtStatusObservationBuilder AddEvidence(string referenceString, string? displayText = null)
public AdtStatusObservationBuilder WithEffectiveDate(DateTime effectiveDate)
public AdtStatusObservationBuilder WithEffectiveDate(DateTimeOffset effectiveDate)
public AdtStatusObservationBuilder AddNote(string noteText)
```

#### Validation Requirements

- Patient reference is required
- Device reference is required
- ADT status (boolean) must be set
- At least one evidence reference is recommended

---

### PsaProgressionObservationBuilder

Creates observations for PSA (Prostate-Specific Antigen) progression analysis.

#### Purpose

Generates FHIR Observations that assess PSA progression using either ThirdOpinion.io or PCWG3 (Prostate Cancer Working
Group 3) criteria.

#### Key Features

- **Category**: `laboratory` (Laboratory study)
- **Code**: LOINC `97509-4` (PSA progression)
- **Value**: Progressive disease vs. Stable disease (SNOMED codes)
- **Criteria Support**: ThirdOpinion.io and PCWG3 methodologies
- **Automatic Calculations**: Percentage and absolute PSA changes
- **Evidence**: PSA measurement references with roles

#### CriteriaType Enum

```csharp
public enum CriteriaType
{
    ThirdOpinionIO,  // ThirdOpinion.io criteria - baseline comparison
    PCWG3           // PCWG3 criteria - nadir comparison with 25% threshold
}
```

#### Basic Usage

```csharp
var psaObservation = new PsaProgressionObservationBuilder(config)
    .WithInferenceId("psa-progression-001")
    .WithPatient("Patient/patient-456", "Robert Johnson")
    .WithDevice("Device/psa-analysis-ai", "PSA Analysis AI v3.0")
    .WithCriteria(CriteriaType.PCWG3, "3.0")
    .AddPsaEvidence("Observation/psa-nadir", "nadir", 1.2m)
    .AddPsaEvidence("Observation/psa-current", "current", 2.8m)  // 133% increase
    .WithProgression(true) // Progressive disease
    .WithMostRecentPsaValue(DateTime.Parse("2024-01-15"), "2.8 ng/mL",
                           "Observation/psa-current")
    .AddThresholdMetComponent(true) // Exceeds 25% PCWG3 threshold
    .WithConfidence(0.89f)
    .WithEffectiveDate(DateTime.UtcNow)
    .Build();
```

#### Example with ThirdOpinion.io Criteria

```csharp
var psaObservation = new PsaProgressionObservationBuilder(config)
    .WithPatient("Patient/patient-789")
    .WithDevice("Device/psa-ai")
    .WithCriteria(CriteriaType.ThirdOpinionIO, "2.1")
    .AddPsaEvidence("Observation/psa-baseline", "baseline", 5.2m)
    .AddPsaEvidence("Observation/psa-current", "current", 6.8m)  // 31% increase
    .WithProgression(false) // Stable disease (below threshold)
    .Build();
```

#### PSA Evidence Roles

- **baseline** - Initial PSA before treatment
- **nadir** - Lowest PSA achieved during treatment
- **current** - Most recent PSA measurement
- **latest** - Alias for current

#### Automatic Calculations

The builder automatically calculates:

- **Percentage change** from baseline/nadir
- **Absolute change** in ng/mL
- **Threshold analysis** for PCWG3 (25% rule)

#### API Reference

```csharp
public PsaProgressionObservationBuilder WithInferenceId(string id)
public PsaProgressionObservationBuilder WithPatient(ResourceReference patientRef)
public PsaProgressionObservationBuilder WithDevice(ResourceReference deviceRef)
public PsaProgressionObservationBuilder WithFocus(params ResourceReference[] focus)
public PsaProgressionObservationBuilder WithCriteria(CriteriaType criteriaType, string version)
public PsaProgressionObservationBuilder AddPsaEvidence(ResourceReference psaObservation, string role, decimal? value = null)
public PsaProgressionObservationBuilder WithProgression(bool hasProgression)
public PsaProgressionObservationBuilder WithMostRecentPsaValue(DateTime mostRecentDateTime, string psaValueText, ResourceReference observationRef)
public PsaProgressionObservationBuilder AddValidUntilComponent(DateTime validUntil)
public PsaProgressionObservationBuilder AddThresholdMetComponent(bool thresholdMet)
public PsaProgressionObservationBuilder AddDetailedAnalysisNote(string analysis)
public PsaProgressionObservationBuilder WithConfidence(float confidence)
public PsaProgressionObservationBuilder WithEffectiveDate(DateTime effectiveDate)
public PsaProgressionObservationBuilder AddNote(string noteText)
```

#### Validation Requirements

- Patient reference is required
- Device reference is required
- Progression status must be set
- At least one PSA evidence reference is required

---

### RadiographicObservationBuilder

**Unified builder for creating FHIR Observations for radiographic progression assessment**

Creates observations for radiographic progression analysis supporting multiple assessment standards: RECIST 1.1, PCWG3, and Observed.

> **Note**: This builder replaces the deprecated `RecistProgressionObservationBuilder` and `Pcwg3ProgressionObservationBuilder` classes, providing a unified interface for all radiographic assessment standards.

#### Purpose

Generates FHIR Observations that assess radiographic progression using your choice of clinical assessment standard. The builder adapts its behavior based on the selected standard while maintaining a consistent, fluent API.

#### Key Features

- **Unified Interface**: Single builder supports multiple radiographic assessment standards
- **Standard Selection**: Choose RECIST 1.1, PCWG3, or Observed via enum parameter
- **Category**: `imaging` (Imaging study)
- **Common Methods**: Core functionality available for all standards
- **Standard-Specific Methods**: Specialized methods for each assessment type
- **Evidence Support**: Imaging studies and radiology reports for all standards
- **Clinical Facts**: Structured supporting and conflicting evidence
- **AI Integration**: Confidence scoring and AIAST security labeling

#### RadiographicStandard Enum

Select the assessment standard using the `RadiographicStandard` enum:

```csharp
public enum RadiographicStandard
{
    /// RECIST 1.1 (Response Evaluation Criteria in Solid Tumors version 1.1)
    /// Used for solid tumor response assessment
    RECIST_1_1,

    /// PCWG3 (Prostate Cancer Working Group 3) bone scan progression criteria
    /// Used for bone scan progression in prostate cancer
    PCWG3,

    /// Observed radiographic progression without specific criteria
    /// Used for general radiographic findings
    Observed
}
```

#### Standard-Specific Codes and Values

Each standard generates appropriate FHIR codes:

**PCWG3 Standard:**
- Code: LOINC `44667-7` (Bone scan findings)
- Value: Progressive disease (`277022003`) or Stable disease (`359746009`)

**RECIST 1.1 Standard:**
- Code: LOINC `21976-6` (Cancer disease status) + NCI `C111544` (RECIST 1.1)
- Value: RECIST response codes (CR, PR, PD, SD) or custom via `WithRecistResponse()`

**Observed Standard:**
- Code: LOINC `59462-2` (Imaging study Observations)
- Value: Progressive disease, Stable disease, or Inconclusive

#### Basic Usage Examples

##### RECIST 1.1 Example

```csharp
var recistObservation = new RadiographicObservationBuilder(config, RadiographicStandard.RECIST_1_1)
    .WithInferenceId("recist-assessment-001")
    .WithPatient("Patient/patient-321", "Michael Davis")
    .WithDevice("Device/recist-ai-classifier", "RECIST AI Classifier v1.5")
    .WithFocus(new ResourceReference("Condition/metastatic-prostate-cancer"))
    .WithRecistResponse("C35571", "Progressive Disease") // PD
    .AddImagingStudy(new ResourceReference("ImagingStudy/ct-chest-001", "CT Chest/Abdomen/Pelvis"))
    .AddRadiologyReport(new ResourceReference("DiagnosticReport/rad-001", "Radiology interpretation"))
    .AddComponent("Current SLD", new Quantity(58.7m, "mm"))
    .AddComponent("New lesion detected", true)
    .WithBodySite("10200004", "Liver structure")
    .WithMeasurementChange("Target lesions increased from 45.2mm to 58.7mm (30% increase)")
    .WithImagingType("CT with IV contrast")
    .WithConfidence(0.92f)
    .WithEffectiveDate(DateTime.UtcNow)
    .AddNote("Progressive disease with 30% increase in target lesions")
    .Build();
```

##### PCWG3 Example

```csharp
var pcwg3Observation = new RadiographicObservationBuilder(config, RadiographicStandard.PCWG3)
    .WithInferenceId("pcwg3-assessment-001")
    .WithPatient("Patient/patient-789", "David Wilson")
    .WithDevice("Device/pcwg3-bone-scan-ai", "PCWG3 Bone Scan AI v2.0")
    .WithFocus(new ResourceReference("Condition/prostate-cancer-with-bone-mets"))
    .WithDetermination("PD") // Progressive Disease
    .WithInitialLesions("New lesion at L5 vertebra")
    .WithConfirmationDate(new DateTime(2025, 2, 15))
    .WithTimeBetweenScans("8 weeks")
    .WithAdditionalLesions("Multiple new thoracic spine lesions")
    .AddImagingStudy(new ResourceReference("ImagingStudy/bone-scan-001"))
    .AddRadiologyReport(new ResourceReference("DiagnosticReport/bone-report-001"))
    .WithSupportingFacts(clinicalFacts)
    .WithConfidence(0.91f)
    .WithEffectiveDate(DateTime.UtcNow)
    .AddNote("PCWG3 criteria met: new bone lesions confirmed on follow-up scan")
    .Build();
```

##### Observed Standard Example

```csharp
var observedProgression = new RadiographicObservationBuilder(config, RadiographicStandard.Observed)
    .WithInferenceId("observed-assessment-001")
    .WithPatient("Patient/patient-456", "Sarah Johnson")
    .WithDevice("Device/radiologist-review", "Radiologist Review")
    .WithFocus(new ResourceReference("Condition/lung-cancer"))
    .WithDetermination("PD") // Progressive Disease observed
    .AddImagingStudy(new ResourceReference("ImagingStudy/pet-ct-001", "PET-CT"))
    .AddRadiologyReport(new ResourceReference("DiagnosticReport/pet-report-001"))
    .WithSummary("Increased FDG uptake in mediastinal lymph nodes suggesting progression")
    .WithConfidence(0.85f)
    .WithEffectiveDate(DateTime.UtcNow)
    .AddNote("Radiographic progression observed without specific criteria application")
    .Build();
```

**Inconclusive RECIST with Observed Changes Example:**

When RECIST criteria are inconclusive but progression is observed:

```csharp
var inconclusiveWithProgression = new RadiographicObservationBuilder(config, RadiographicStandard.Observed)
    .WithInferenceId("observed-assessment-002")
    .WithPatient("Patient/patient-789", "John Smith")
    .WithDevice("Device/radiologist-review", "Radiologist Review")
    .WithFocus(new ResourceReference("Condition/prostate-cancer"))
    .WithDetermination("Inconclusive") // RECIST criteria inconclusive
    .WithObservedChanges("Progression") // But progression is clearly observed
    .AddImagingStudy(new ResourceReference("ImagingStudy/ct-456", "CT Scan"))
    .WithSummary("RECIST criteria inconclusive due to measurement variability, but clear progression observed")
    .WithConfidence(0.80f)
    .Build();
```

#### Common Methods (Available for All Standards)

These methods work with all three assessment standards:

```csharp
// Core setup
.WithInferenceId(string id)
.WithPatient(ResourceReference patient)
.WithPatient(string patientId, string? display = null)
.WithDevice(ResourceReference device)
.WithDevice(string deviceId, string? display = null)
.WithFocus(params ResourceReference[] focuses)

// Assessment details
.WithDetermination(string determination)  // "CR", "PR", "SD", "PD", "Baseline", or "Inconclusive"
.WithConfidence(float confidence)         // 0.0 to 1.0
.WithConfidenceRationale(string? rationale)
.WithConfirmationDate(DateTime? date)
.WithSummary(string? summary)

// Evidence - Available for ALL standards
.AddImagingStudy(ResourceReference imagingStudy)
.AddRadiologyReport(ResourceReference report)
.WithSupportingFacts(params Fact[] facts)
.WithConflictingFacts(params Fact[] facts)

// Metadata
.WithEffectiveDate(DateTime effectiveDate)
.WithEffectiveDate(DateTimeOffset effectiveDate)
.AddNote(string noteText)

// Base class methods
.WithCriteria(string id, string display, string? system = null)
.AddDerivedFrom(ResourceReference reference)
.AddDerivedFrom(string reference, string? display = null)
```

#### PCWG3-Specific Methods

Additional methods available when using `RadiographicStandard.PCWG3`:

```csharp
.WithInitialLesions(string? initialLesions)
.WithAdditionalLesions(string? additionalLesions)
.WithTimeBetweenScans(string? timeBetweenScans)
.WithInitialScanDate(DateTime? initialScanDate)
.WithConfirmationLesions(string? confirmationLesions)
.AddEvidence(ResourceReference reference, string? display = null)
.AddEvidence(string referenceString, string? display = null)
```

**Example:**
```csharp
var pcwg3Obs = new RadiographicObservationBuilder(config, RadiographicStandard.PCWG3)
    .WithPatient("Patient/patient-001")
    .WithDevice("Device/bone-scan-ai")
    .WithDetermination("PD")
    .WithInitialLesions("2 new bone lesions at L3 and T10")
    .WithConfirmationDate(DateTime.Parse("2025-02-01"))
    .WithTimeBetweenScans("8 weeks")
    .WithAdditionalLesions("1 additional lesion at T8 on confirmation scan")
    .WithConfidence(0.93f)
    .Build();
```

#### Observed-Specific Methods

Additional methods available when using `RadiographicStandard.Observed`:

```csharp
.WithObservedChanges(string? observedChanges)
```

**Purpose:** Describes observed radiographic changes when formal criteria (like RECIST) are inconclusive but changes are evident. Values are automatically mapped to SNOMED CT codes.

**SNOMED Code Mapping:**
The method automatically maps common values to standardized SNOMED CT codes:
- `"Progression"` → **444391001** "Malignant tumor progression (finding)"
- `"Stable"` → **713837000** "Neoplasm stable (finding)"
- `"Regression"` → **265743007** "Regression of neoplasm (finding)"
- Other values → Stored as text-only CodeableConcept

The mapping is case-insensitive, so "progression", "Progression", and "PROGRESSION" all map to the same SNOMED code.

**Example:**
```csharp
var observedObs = new RadiographicObservationBuilder(config, RadiographicStandard.Observed)
    .WithPatient("Patient/patient-003")
    .WithDevice("Device/radiologist-review")
    .WithDetermination("Inconclusive") // Formal criteria unclear, uses SNOMED 419984006
    .WithObservedChanges("Progression") // Maps to SNOMED 444391001
    .WithSummary("Clear progression observed despite measurement variability")
    .WithConfidence(0.85f)
    .Build();
```

**Use Cases:**
- RECIST criteria inconclusive due to measurement variability, but visual progression clear
- Mixed response (some lesions progress, others stable)
- New lesions below size threshold but clinically significant
- Qualitative changes not captured by formal criteria

#### RECIST-Specific Methods

Additional methods available when using `RadiographicStandard.RECIST_1_1`:

```csharp
.WithRecistCriteria(string criteria)
.WithRecistResponse(string nciCode, string display)
.WithBodySite(string snomedCode, string display)
.WithMeasurementChange(string? measurementChange)
.WithImagingType(string? imagingType)
.WithImagingDate(DateTime? imagingDate)
.WithRecistTimepointsJson(string? timepointsJson)
.AddComponent(string code, Quantity value)
.AddComponent(string code, bool value)
.AddComponent(string code, CodeableConcept value)
```

**RECIST Response Codes:**
- **CR** (Complete Response) - `C25197` - No evidence of disease
- **PR** (Partial Response) - `C25206` - ≥30% decrease in SLD
- **PD** (Progressive Disease) - `C35571` - ≥20% increase in SLD or new lesions
- **SD** (Stable Disease) - `C85553` - Neither PR nor PD criteria met

**Example:**
```csharp
var recistObs = new RadiographicObservationBuilder(config, RadiographicStandard.RECIST_1_1)
    .WithPatient("Patient/patient-002")
    .WithDevice("Device/recist-analyzer")
    .WithRecistResponse("C35571", "Progressive Disease")
    .WithBodySite("39607008", "Lung structure")
    .AddComponent("Target lesion SLD", new Quantity(58.7m, "mm"))
    .AddComponent("Percent change from nadir", new Quantity(30.0m, "%"))
    .AddComponent("New lesion detected", true)
    .WithMeasurementChange("SLD increased from 45.2mm to 58.7mm")
    .WithImagingType("CT Chest/Abdomen/Pelvis")
    .WithImagingDate(DateTime.Parse("2025-01-15"))
    .WithConfidence(0.94f)
    .Build();
```

#### RECIST Timepoints JSON Storage

For RECIST assessments, store complete timepoint data structures:

```csharp
var timepointsJson = @"{
    ""baseline"": {
        ""date"": ""2024-06-01"",
        ""targetLesions"": [
            { ""location"": ""Lung, right upper lobe"", ""diameter"": 32.5 },
            { ""location"": ""Liver, segment 7"", ""diameter"": 12.7 }
        ],
        ""sld"": 45.2
    },
    ""followup"": {
        ""date"": ""2025-01-15"",
        ""targetLesions"": [
            { ""location"": ""Lung, right upper lobe"", ""diameter"": 42.1 },
            { ""location"": ""Liver, segment 7"", ""diameter"": 16.6 }
        ],
        ""sld"": 58.7,
        ""newLesions"": [
            { ""location"": ""Liver, segment 4"", ""diameter"": 8.2 }
        ]
    }
}";

var observation = new RadiographicObservationBuilder(config, RadiographicStandard.RECIST_1_1)
    .WithRecistTimepointsJson(timepointsJson)
    .WithRecistResponse("C35571", "Progressive Disease")
    // ... other methods
    .Build();
```

#### Supporting Facts Integration

All standards support structured clinical facts as evidence:

```csharp
var supportingFacts = new[]
{
    new Fact
    {
        factGuid = "fact-001",
        factDocumentReference = "DocumentReference/baseline-scan",
        type = "finding",
        fact = "Baseline imaging showed 3 target lesions with SLD 45.2mm",
        @ref = new[] { "1.123" },
        timeRef = "2024-06-01",
        relevance = "Establishes baseline for comparison"
    },
    new Fact
    {
        factGuid = "fact-002",
        factDocumentReference = "DocumentReference/followup-scan",
        type = "finding",
        fact = "Follow-up imaging shows SLD increased to 58.7mm (30% increase)",
        @ref = new[] { "2.456" },
        timeRef = "2025-01-15",
        relevance = "Documents progression per RECIST criteria"
    }
};

var observation = builder
    .WithSupportingFacts(supportingFacts)
    .Build();
```

#### API Reference

##### Constructor
```csharp
public RadiographicObservationBuilder(
    AiInferenceConfiguration configuration,
    RadiographicStandard standard)
```

##### Common Methods (All Standards)
```csharp
public RadiographicObservationBuilder WithInferenceId(string id)
public RadiographicObservationBuilder WithPatient(ResourceReference patient)
public RadiographicObservationBuilder WithPatient(string patientId, string? display = null)
public RadiographicObservationBuilder WithDevice(ResourceReference device)
public RadiographicObservationBuilder WithDevice(string deviceId, string? display = null)
public RadiographicObservationBuilder WithFocus(params ResourceReference[] focuses)
public RadiographicObservationBuilder WithDetermination(string? determination)
public RadiographicObservationBuilder WithConfidence(float confidence)
public RadiographicObservationBuilder WithConfidenceRationale(string? rationale)
public RadiographicObservationBuilder WithConfirmationDate(DateTime? date)
public RadiographicObservationBuilder WithSummary(string? summary)
public RadiographicObservationBuilder AddImagingStudy(ResourceReference imagingStudy)
public RadiographicObservationBuilder AddRadiologyReport(ResourceReference report)
public RadiographicObservationBuilder WithSupportingFacts(params Fact[] facts)
public RadiographicObservationBuilder WithConflictingFacts(params Fact[] facts)
public RadiographicObservationBuilder WithEffectiveDate(DateTime effectiveDate)
public RadiographicObservationBuilder WithEffectiveDate(DateTimeOffset effectiveDate)
public RadiographicObservationBuilder AddNote(string noteText)
```

##### PCWG3-Specific Methods
```csharp
public RadiographicObservationBuilder WithInitialLesions(string? initialLesions)
public RadiographicObservationBuilder WithAdditionalLesions(string? additionalLesions)
public RadiographicObservationBuilder WithTimeBetweenScans(string? timeBetweenScans)
public RadiographicObservationBuilder WithInitialScanDate(DateTime? initialScanDate)
public RadiographicObservationBuilder WithConfirmationLesions(string? confirmationLesions)
public RadiographicObservationBuilder AddEvidence(ResourceReference reference, string? display = null)
public RadiographicObservationBuilder AddEvidence(string referenceString, string? display = null)
```

##### Observed-Specific Methods
```csharp
public RadiographicObservationBuilder WithObservedChanges(string? observedChanges)
```

##### RECIST-Specific Methods
```csharp
public RadiographicObservationBuilder WithRecistCriteria(string criteria)
public RadiographicObservationBuilder WithRecistResponse(string nciCode, string display)
public RadiographicObservationBuilder WithBodySite(string snomedCode, string display)
public RadiographicObservationBuilder WithMeasurementChange(string? measurementChange)
public RadiographicObservationBuilder WithImagingType(string? imagingType)
public RadiographicObservationBuilder WithImagingDate(DateTime? imagingDate)
public RadiographicObservationBuilder WithRecistTimepointsJson(string? timepointsJson)
public RadiographicObservationBuilder AddComponent(string code, Quantity value)
public RadiographicObservationBuilder AddComponent(string code, bool value)
public RadiographicObservationBuilder AddComponent(string code, CodeableConcept value)
```

#### Validation Requirements

All standards require:
- Patient reference (call `WithPatient()`)
- Device reference (call `WithDevice()`)

Standard-specific requirements:
- **RECIST 1.1**: At least one component or RECIST response recommended
- **PCWG3**: Determination (CR/PR/SD/PD/Baseline/Inconclusive) or supporting facts recommended
- **Observed**: Summary or notes recommended to document findings

#### Migration Guide

Migrating from deprecated builders is straightforward:

**From RecistProgressionObservationBuilder:**
```csharp
// Old code
var observation = new RecistProgressionObservationBuilder(config)
    .WithPatient(...)
    .WithRecistResponse(...)
    .Build();

// New code - Add standard parameter to constructor
var observation = new RadiographicObservationBuilder(config, RadiographicStandard.RECIST_1_1)
    .WithPatient(...)
    .WithRecistResponse(...)
    .Build();
```

**From Pcwg3ProgressionObservationBuilder:**
```csharp
// Old code
var observation = new Pcwg3ProgressionObservationBuilder(config)
    .WithPatient(...)
    .WithIdentified(true)
    .Build();

// New code - Add standard parameter, use WithDetermination with RECIST codes
var observation = new RadiographicObservationBuilder(config, RadiographicStandard.PCWG3)
    .WithPatient(...)
    .WithDetermination("PD")  // Replaces WithIdentified(true), uses Progressive Disease
    .Build();
```

**Key Changes:**
- Add `RadiographicStandard` enum parameter to constructor
- `WithIdentified(bool)` is now `WithDetermination()` with RECIST response codes: CR, PR, SD, PD, Baseline, or Inconclusive
- `AddImagingStudy()` and `AddRadiologyReport()` now available for **all** standards
- All other method names remain the same

## Common Patterns

### Error Handling

```csharp
try
{
    var observation = builder.Build();
}
catch (InvalidOperationException ex) when (ex.Message.Contains("Patient"))
{
    // Handle missing patient reference
    logger.LogError("Patient reference is required for observation");
}
catch (InvalidOperationException ex) when (ex.Message.Contains("progression"))
{
    // Handle missing progression status
    logger.LogError("Progression status must be set");
}
```

### Confidence Scoring

All observation builders support AI confidence scoring:

```csharp
var observation = builder
    .WithConfidence(0.92f) // 92% confidence
    .Build();

// Results in component:
{
  "code": { "text": "AI Confidence Score" },
  "valueQuantity": {
    "value": 0.92,
    "unit": "probability"
  }
}
```

### Evidence References

Link observations to supporting evidence:

```csharp
var observation = builder
    .AddEvidence("Observation/psa-123", "PSA measurement")
    .AddEvidence("DocumentReference/report-456", "Pathology report")
    .AddEvidence("ImagingStudy/ct-789", "CT scan")
    .Build();
```

### Multi-Criteria Assessments

For complex cases requiring multiple assessment methods:

```csharp
// PCWG3 PSA Assessment
var pcwg3Assessment = new PsaProgressionObservationBuilder(config)
    .WithCriteria(CriteriaType.PCWG3, "3.0")
    .AddPsaEvidence(nadir, "nadir", 1.5m)
    .AddPsaEvidence(current, "current", 3.2m) // 113% increase
    .WithProgression(true)
    .Build();

// ThirdOpinion.io PSA Assessment
var thirdOpinionAssessment = new PsaProgressionObservationBuilder(config)
    .WithCriteria(CriteriaType.ThirdOpinionIO, "2.1")
    .AddPsaEvidence(baseline, "baseline", 8.7m)
    .AddPsaEvidence(current, "current", 3.2m) // 63% decrease
    .WithProgression(false)
    .Build();
```

### Temporal Analysis

Track assessments over time:

```csharp
var followUpObservation = builder
    .AddValidUntilComponent(DateTime.UtcNow.AddMonths(3)) // Valid for 3 months
    .WithMostRecentPsaValue(DateTime.Parse("2024-01-15"), "3.2 ng/mL", psaRef)
    .AddDetailedAnalysisNote("Trend analysis shows continued response to treatment")
    .Build();
```

## Integration Examples

### Lambda Function Integration

```csharp
public class PsaAnalysisFunction
{
    private readonly AiInferenceConfiguration _config;

    public async Task<APIGatewayProxyResponse> HandleAsync(
        APIGatewayProxyRequest request)
    {
        var patientId = request.PathParameters["patientId"];
        var psaData = await GetPsaDataAsync(patientId);

        var observation = new PsaProgressionObservationBuilder(_config)
            .WithPatient($"Patient/{patientId}")
            .WithDevice("Device/lambda-psa-analyzer")
            .WithCriteria(CriteriaType.PCWG3, "3.0")
            .AddPsaEvidence(psaData.NadirRef, "nadir", psaData.NadirValue)
            .AddPsaEvidence(psaData.CurrentRef, "current", psaData.CurrentValue)
            .WithProgression(psaData.HasProgression)
            .WithConfidence(psaData.Confidence)
            .Build();

        return new APIGatewayProxyResponse
        {
            StatusCode = 200,
            Body = observation.ToJson(),
            Headers = new Dictionary<string, string>
            {
                ["Content-Type"] = "application/fhir+json"
            }
        };
    }
}
```

### Batch Processing

```csharp
public async Task ProcessPatientsAsync(IEnumerable<string> patientIds)
{
    var tasks = patientIds.Select(async patientId =>
    {
        var builder = new AdtStatusObservationBuilder(_config);
        var adtData = await AnalyzeAdtStatusAsync(patientId);

        return builder
            .WithPatient($"Patient/{patientId}")
            .WithDevice("Device/batch-adt-analyzer")
            .WithStatus(adtData.IsOnAdt, adtData.Confidence)
            .AddEvidence(adtData.EvidenceRefs.ToArray())
            .Build();
    });

    var observations = await Task.WhenAll(tasks);
    await SaveObservationsAsync(observations);
}
```

## Best Practices

### Builder Selection

- **AdtStatusObservationBuilder** - For treatment status detection
- **PsaProgressionObservationBuilder** - For biochemical progression analysis
- **RecistProgressionObservationBuilder** - For radiographic progression analysis

### Performance Tips

- Reuse `AiInferenceConfiguration` instances
- Cache frequently used ResourceReferences
- Use batch operations for multiple patients
- Consider async patterns for I/O operations

### Testing

```csharp
[Fact]
public void PsaBuilder_WithPCWG3Criteria_CalculatesCorrectly()
{
    var observation = new PsaProgressionObservationBuilder(_config)
        .WithPatient(_patientRef)
        .WithDevice(_deviceRef)
        .WithCriteria(CriteriaType.PCWG3, "3.0")
        .AddPsaEvidence(_nadirRef, "nadir", 2.0m)
        .AddPsaEvidence(_currentRef, "current", 3.0m) // 50% increase
        .WithProgression(true)
        .Build();

    // Verify calculation components
    var percentageComponent = observation.Component
        .FirstOrDefault(c => c.Code.Text == "PSA Percentage Change");

    percentageComponent.ShouldNotBeNull();
    ((Quantity)percentageComponent.Value).Value.ShouldBe(50m);
}
```