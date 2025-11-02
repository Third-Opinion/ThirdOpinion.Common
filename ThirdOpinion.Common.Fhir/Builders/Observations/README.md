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

### RecistProgressionObservationBuilder

Creates observations for RECIST 1.1 radiographic progression analysis.

#### Purpose

Generates FHIR Observations that assess radiographic progression using RECIST 1.1 (Response Evaluation Criteria in Solid
Tumors) methodology.

#### Key Features

- **Category**: `imaging` (Imaging study)
- **Code**: LOINC `33717-0` (Tumor response)
- **Value**: RECIST response codes (Complete Response, Partial Response, Progressive Disease, Stable Disease)
- **Evidence**: Imaging studies and radiology reports
- **Measurements**: Target lesion diameters (SLD)
- **Body Sites**: Anatomical locations using SNOMED codes

#### RECIST Response Types

- **CR** (Complete Response) - `C25197` - No evidence of disease
- **PR** (Partial Response) - `C25206` - ≥30% decrease in SLD
- **PD** (Progressive Disease) - `C35571` - ≥20% increase in SLD or new lesions
- **SD** (Stable Disease) - `C85553` - Neither PR nor PD criteria met

#### Basic Usage

```csharp
var recistObservation = new RecistProgressionObservationBuilder(config)
    .WithInferenceId("recist-assessment-001")
    .WithPatient("Patient/patient-321", "Michael Davis")
    .WithDevice("Device/recist-ai-classifier", "RECIST AI Classifier v1.5")
    .WithFocus("Condition/metastatic-prostate-cancer")
    .WithCriteria("RECIST-1.1-2024", "RECIST 1.1 Criteria",
                  "Response Evaluation Criteria in Solid Tumors version 1.1")
    .WithRecistResponse("C35571", "Progressive Disease") // PD
    .AddImagingStudy("ImagingStudy/ct-chest-abdomen-001", "CT Chest/Abdomen/Pelvis")
    .AddRadiologyReport("DocumentReference/radiology-report-001", "Radiology interpretation")
    .AddComponent("Nadir SLD", new Quantity(45.2m, "mm"))
    .AddComponent("Current SLD", new Quantity(58.7m, "mm"))  // 30% increase
    .AddComponent("New lesion detected", true)
    .AddBodySite("10200004", "Liver structure") // New liver lesion
    .AddBodySite("39607008", "Lung structure") // Existing lung lesions
    .WithEffectiveDate(DateTime.UtcNow)
    .AddNote("Progressive disease with 30% increase in target lesions and new liver lesion")
    .Build();
```

#### Component Types

The builder supports various component measurements:

```csharp
// Quantity measurements
.AddComponent("Target lesion SLD", new Quantity(42.5m, "mm"))
.AddComponent("Percent change from nadir", new Quantity(25.3m, "%"))

// Boolean findings
.AddComponent("New lesion detected", true)
.AddComponent("Non-target lesion progression", false)

// CodeableConcept classifications
.AddComponent("Lesion type", new CodeableConcept(...))
```

#### API Reference

```csharp
public RecistProgressionObservationBuilder WithInferenceId(string id)
public RecistProgressionObservationBuilder WithPatient(ResourceReference patientRef)
public RecistProgressionObservationBuilder WithDevice(ResourceReference deviceRef)
public RecistProgressionObservationBuilder WithFocus(ResourceReference conditionRef)
public RecistProgressionObservationBuilder WithCriteria(string criteriaId, string display, string description)
public RecistProgressionObservationBuilder WithRecistResponse(string nciCode, string display)
public RecistProgressionObservationBuilder AddComponent(string codeText, Quantity valueQuantity)
public RecistProgressionObservationBuilder AddComponent(string codeText, bool valueBoolean)
public RecistProgressionObservationBuilder AddComponent(string codeText, CodeableConcept valueCodeableConcept)
public RecistProgressionObservationBuilder AddImagingStudy(ResourceReference imagingStudyRef, string? displayText = null)
public RecistProgressionObservationBuilder AddRadiologyReport(ResourceReference documentRef, string? displayText = null)
public RecistProgressionObservationBuilder AddBodySite(string snomedCode, string display)
public RecistProgressionObservationBuilder WithEffectiveDate(DateTime effectiveDate)
public RecistProgressionObservationBuilder AddNote(string noteText)
```

#### Validation Requirements

- Patient reference is required
- Device reference is required
- RECIST response must be set
- At least one component measurement is recommended
- Focus reference to primary condition is recommended

---

### Pcwg3ProgressionObservationBuilder

Creates observations for PCWG3 bone scan progression analysis in prostate cancer.

#### Purpose

Generates FHIR Observations that assess bone scan progression using PCWG3 (Prostate Cancer Working Group 3) criteria for
bone metastases progression.

#### Key Features

- **Category**: `imaging` (Imaging study)
- **Code**: LOINC `44667-7` (Bone scan findings)
- **Value**: Progressive disease vs. Stable disease (SNOMED codes)
- **Evidence**: Supporting clinical facts with structured metadata
- **AI Integration**: Confidence scoring and AIAST security labeling
- **PCWG3 Criteria**: Specialized bone scan progression assessment

#### PCWG3 Progression Logic

- **Progression**: New bone lesions or unequivocal progression of existing lesions
- **Stable**: No new lesions and no unequivocal progression
- **Confirmation**: Requires confirmation scan ≥6 weeks after initial detection

#### Basic Usage

```csharp
var pcwg3Observation = new Pcwg3ProgressionObservationBuilder(config)
    .WithInferenceId("pcwg3-assessment-001")
    .WithPatient("Patient/patient-789", "David Wilson")
    .WithDevice("Device/pcwg3-bone-scan-ai", "PCWG3 Bone Scan AI v2.0")
    .WithFocus("Condition/prostate-cancer-with-bone-mets")
    .WithIdentified(true) // Progression identified
    .WithInitialLesions("New lesion at L5 vertebra")
    .WithConfirmationDate(new DateTime(2025, 2, 15))
    .WithTimeBetweenScans("8 weeks")
    .WithAdditionalLesions("Multiple new thoracic spine lesions")
    .WithSupportingFacts(clinicalFacts)
    .WithConfidence(0.91f)
    .WithEffectiveDate(DateTime.UtcNow)
    .AddNote("PCWG3 criteria met: new bone lesions confirmed on follow-up scan")
    .Build();
```

#### Supporting Facts Integration

The builder accepts clinical facts that provide evidence for progression:

```csharp
var supportingFacts = new[]
{
    new Fact
    {
        factGuid = "fact-001",
        factDocumentReference = "DocumentReference/bone-scan-baseline",
        type = "finding",
        fact = "Baseline bone scan showed no evidence of metastatic disease",
        @ref = new[] { "1.123" },
        timeRef = "2024-12-01",
        relevance = "Establishes baseline for progression assessment"
    },
    new Fact
    {
        factGuid = "fact-002",
        factDocumentReference = "DocumentReference/bone-scan-followup",
        type = "finding",
        fact = "Follow-up scan shows new uptake in L5 and T8 vertebrae",
        @ref = new[] { "2.456" },
        timeRef = "2025-02-15",
        relevance = "Evidence of new bone metastases indicating progression"
    }
};

var observation = builder
    .WithSupportingFacts(supportingFacts)
    .Build();
```

#### Component Structure

The builder creates structured components for:

- **Initial Lesions**: Description of newly identified lesions
- **Confirmation Date**: Date progression was confirmed
- **Time Between Scans**: Interval between initial and confirmation scans
- **Additional Lesions**: Further lesions beyond initial findings
- **Confidence Score**: AI assessment confidence (0.0-1.0)

#### API Reference

```csharp
public Pcwg3ProgressionObservationBuilder WithInferenceId(string id)
public Pcwg3ProgressionObservationBuilder WithPatient(ResourceReference patientRef)
public Pcwg3ProgressionObservationBuilder WithPatient(string patientId, string? display = null)
public Pcwg3ProgressionObservationBuilder WithDevice(ResourceReference deviceRef)
public Pcwg3ProgressionObservationBuilder WithDevice(string deviceId, string? display = null)
public Pcwg3ProgressionObservationBuilder WithFocus(params ResourceReference[] focus)
public Pcwg3ProgressionObservationBuilder WithIdentified(bool identified)
public Pcwg3ProgressionObservationBuilder WithInitialLesions(string? initialLesions)
public Pcwg3ProgressionObservationBuilder WithConfirmationDate(DateTime? confirmationDate)
public Pcwg3ProgressionObservationBuilder WithTimeBetweenScans(string? timeBetweenScans)
public Pcwg3ProgressionObservationBuilder WithAdditionalLesions(string? additionalLesions)
public Pcwg3ProgressionObservationBuilder WithSupportingFacts(params Fact[] facts)
public Pcwg3ProgressionObservationBuilder WithConfidence(float confidence)
public Pcwg3ProgressionObservationBuilder WithEffectiveDate(DateTime effectiveDate)
public Pcwg3ProgressionObservationBuilder AddNote(string noteText)
```

#### Progressive Disease Example

```csharp
var progressionObservation = new Pcwg3ProgressionObservationBuilder(config)
    .WithPatient("Patient/pc-patient-001")
    .WithDevice("Device/pcwg3-analyzer")
    .WithFocus("Condition/prostate-cancer-stage-iv")
    .WithIdentified(true) // Progression detected
    .WithInitialLesions("New focal uptake L5 vertebral body")
    .WithConfirmationDate(new DateTime(2025, 3, 15))
    .WithTimeBetweenScans("12 weeks")
    .WithAdditionalLesions("Increased uptake T8, new lesion T12")
    .WithConfidence(0.94f)
    .Build();

// Results in SNOMED 277022003 "Progressive disease"
```

#### Stable Disease Example

```csharp
var stableObservation = new Pcwg3ProgressionObservationBuilder(config)
    .WithPatient("Patient/pc-patient-002")
    .WithDevice("Device/pcwg3-analyzer")
    .WithFocus("Condition/prostate-cancer-stage-iv")
    .WithIdentified(false) // No progression
    .WithConfidence(0.88f)
    .Build();

// Results in SNOMED 359746009 "Stable disease"
```

#### Enhanced RecistProgressionObservationBuilder Features

The RecistProgressionObservationBuilder has been enhanced with new capabilities:

##### New Methods for Enhanced JSON Support

```csharp
public RecistProgressionObservationBuilder WithIdentified(bool identified)
public RecistProgressionObservationBuilder WithMeasurementChange(string? measurementChange)
public RecistProgressionObservationBuilder WithImagingType(string? imagingType)
public RecistProgressionObservationBuilder WithConfirmationDate(DateTime? confirmationDate)
public RecistProgressionObservationBuilder WithSupportingFacts(params Fact[] facts)
public RecistProgressionObservationBuilder WithConfidence(float confidence)
```

##### Enhanced Usage Example

```csharp
var enhancedRecistObservation = new RecistProgressionObservationBuilder(config)
    .WithPatient("Patient/patient-123")
    .WithDevice("Device/recist-ai-v2")
    .WithFocus("Condition/nsclc-stage-iv")
    .WithIdentified(true) // Progression identified
    .WithMeasurementChange("Target lesions increased from 45.2mm to 58.7mm (30% increase)")
    .WithImagingType("CT Chest/Abdomen/Pelvis with IV contrast")
    .WithConfirmationDate(new DateTime(2025, 2, 20))
    .WithSupportingFacts(radiologyFacts)
    .WithConfidence(0.92f)
    .WithRecistResponse(FhirCodingHelper.NciCodes.PROGRESSIVE_DISEASE, "Progressive Disease")
    .Build();
```

#### Validation Requirements

- Patient reference is required
- Device reference is required
- Identified status (boolean) must be set
- Confidence must be between 0.0 and 1.0
- Supporting facts should include relevant clinical evidence

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