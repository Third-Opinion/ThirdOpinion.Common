# HSDM Assessment Condition Builder

The `HsdmAssessmentConditionBuilder` creates FHIR Condition resources for **H**ormone **S**ensitivity **D**iagnosis **M
**odifier (HSDM) assessments of Castration-Sensitive Prostate Cancer (CSPC).

## Overview

This builder generates FHIR Condition resources that classify prostate cancer hormone sensitivity status using
standardized SNOMED and ICD-10 codes. It supports three classification types with proper clinical fact evidence
integration.

## Supported HSDM Result Types

| Result Type                  | Description                             | SNOMED Code | ICD-10 Codes   |
|------------------------------|-----------------------------------------|-------------|----------------|
| `nmCSPC_biochemical_relapse` | Non-metastatic with biochemical relapse | 1197209002  | Z19.1 + R97.21 |
| `mCSPC`                      | Metastatic castration-sensitive         | 1197209002  | Z19.1          |
| `mCRPC`                      | Metastatic castration-resistant         | 445848006   | Z19.2          |

## Required Dependencies

```csharp
using Hl7.Fhir.Model;
using ThirdOpinion.Common.Fhir.Builders.Conditions;
using ThirdOpinion.Common.Fhir.Configuration;
using ThirdOpinion.Common.Fhir.Models;
```

## Basic Usage Example

```csharp
// Create configuration
var config = AiInferenceConfiguration.CreateDefault();

// Define clinical facts
var facts = new[]
{
    new Fact
    {
        factGuid = "cc58eb7a-2417-4dab-8782-ec1c99315fd2",
        factDocumentReference = "DocumentReference/pathology-report-001",
        type = "diagnosis",
        fact = "Metastatic adenocarcinoma of prostate with bone involvement",
        @ref = new[] { "Section 2.1", "Figure 3" },
        timeRef = "2024-01-15",
        relevance = "Confirms metastatic disease status for HSDM classification"
    },
    new Fact
    {
        factGuid = "bb47da6b-3528-5cde-9893-fd2d10426ae3",
        factDocumentReference = "DocumentReference/lab-results-003",
        type = "lab",
        fact = "Testosterone level 0.8 ng/mL (castrate level)",
        @ref = new[] { "Lab ID: PSA-789" },
        timeRef = "2024-01-10",
        relevance = "Demonstrates castrate testosterone levels supporting sensitivity classification"
    }
};

// Build HSDM Assessment Condition
var builder = new HsdmAssessmentConditionBuilder(config);

var condition = builder
    .WithInferenceId("hsdm-assessment-001")
    .WithPatient("Patient/prostate-patient-123", "John Smith")
    .WithDevice("Device/ai-hsdm-classifier-v2", "HSDM AI Classifier v2.1")
    .WithFocus("Condition/prostate-cancer-primary", "Primary Prostate Adenocarcinoma")
    .WithCriteria("HSDM-CRITERIA-2024", "HSDM Classification Criteria v2024",
                  "Standardized criteria for hormone sensitivity determination in prostate cancer")
    .WithHSDMResult(HsdmAssessmentConditionBuilder.HsdmResults.MetastaticCastrationSensitive)
    .AddFactEvidence(facts)
    .AddEvidence("Observation/testosterone-level-001", "Castrate testosterone level")
    .AddEvidence("Observation/psa-trend-analysis", "PSA response to ADT")
    .WithSummary("Patient demonstrates metastatic castration-sensitive prostate cancer based on " +
                 "radiographic evidence of bone metastases and maintained PSA response to ADT therapy")
    .WithConfidence(0.92f)
    .WithEffectiveDate(DateTime.UtcNow)
    .Build();
```

## Example Output JSON Structure

The above code generates a FHIR Condition resource like this:

```json
{
  "resourceType": "Condition",
  "id": "hsdm-assessment-001",
  "meta": {
    "security": [
      {
        "system": "http://terminology.hl7.org/CodeSystem/v3-ActCode",
        "code": "AIAST",
        "display": "AI Assisted"
      }
    ]
  },
  "clinicalStatus": {
    "coding": [
      {
        "system": "http://terminology.hl7.org/CodeSystem/condition-clinical",
        "code": "active",
        "display": "Active"
      }
    ]
  },
  "verificationStatus": {
    "coding": [
      {
        "system": "http://terminology.hl7.org/CodeSystem/condition-ver-status",
        "code": "confirmed",
        "display": "Confirmed"
      }
    ]
  },
  "category": [
    {
      "coding": [
        {
          "system": "http://terminology.hl7.org/CodeSystem/condition-category",
          "code": "encounter-diagnosis",
          "display": "Encounter Diagnosis"
        }
      ]
    }
  ],
  "code": {
    "coding": [
      {
        "system": "http://snomed.info/sct",
        "code": "1197209002",
        "display": "Castration-sensitive prostate cancer"
      },
      {
        "system": "http://hl7.org/fhir/sid/icd-10-cm",
        "code": "Z19.1",
        "display": "Hormone sensitive malignancy status"
      }
    ],
    "text": "Castration-Sensitive Prostate Cancer (mCSPC)"
  },
  "subject": {
    "reference": "Patient/prostate-patient-123",
    "display": "John Smith"
  },
  "recordedDate": "2024-01-15T14:30:00Z",
  "recorder": {
    "reference": "Device/ai-hsdm-classifier-v2",
    "display": "HSDM AI Classifier v2.1"
  },
  "evidence": [
    {
      "detail": [
        {
          "reference": "DocumentReference/pathology-report-001",
          "display": "Fact evidence: diagnosis"
        }
      ]
    },
    {
      "detail": [
        {
          "reference": "DocumentReference/lab-results-003",
          "display": "Fact evidence: lab"
        }
      ]
    },
    {
      "detail": [
        {
          "reference": "Observation/testosterone-level-001",
          "display": "Castrate testosterone level"
        }
      ]
    }
  ],
  "note": [
    {
      "time": "2024-01-15T14:30:00Z",
      "text": "Patient demonstrates metastatic castration-sensitive prostate cancer based on radiographic evidence of bone metastases and maintained PSA response to ADT therapy"
    }
  ],
  "extension": [
    {
      "url": "https://thirdopinion.io/clinical-fact",
      "extension": [
        {
          "url": "factGuid",
          "valueString": "cc58eb7a-2417-4dab-8782-ec1c99315fd2"
        },
        {
          "url": "factDocumentReference",
          "valueString": "DocumentReference/pathology-report-001"
        },
        {
          "url": "type",
          "valueString": "diagnosis"
        },
        {
          "url": "fact",
          "valueString": "Metastatic adenocarcinoma of prostate with bone involvement"
        },
        {
          "url": "ref",
          "valueString": "Section 2.1"
        },
        {
          "url": "ref",
          "valueString": "Figure 3"
        },
        {
          "url": "timeRef",
          "valueString": "2024-01-15"
        },
        {
          "url": "relevance",
          "valueString": "Confirms metastatic disease status for HSDM classification"
        }
      ]
    },
    {
      "url": "http://thirdopinion.ai/fhir/StructureDefinition/confidence",
      "valueDecimal": 0.92
    },
    {
      "url": "http://thirdopinion.ai/fhir/StructureDefinition/assessment-criteria",
      "extension": [
        {
          "url": "id",
          "valueString": "HSDM-CRITERIA-2024"
        },
        {
          "url": "display",
          "valueString": "HSDM Classification Criteria v2024"
        },
        {
          "url": "description",
          "valueString": "Standardized criteria for hormone sensitivity determination in prostate cancer"
        }
      ]
    }
  ]
}
```

## Biochemical Relapse Example

For non-metastatic biochemical relapse cases, the condition includes both Z19.1 and R97.21 codes:

```csharp
var biochemicalRelapseCondition = builder
    .WithFocus("Condition/prostate-cancer-primary")
    .WithPatient("Patient/patient-456")
    .WithDevice("Device/ai-classifier")
    .WithHSDMResult(HsdmAssessmentConditionBuilder.HsdmResults.NonMetastaticBiochemicalRelapse)
    .AddFactEvidence(psaRisingFacts)
    .WithSummary("Patient shows biochemical relapse with rising PSA following definitive treatment")
    .Build();
```

This generates a condition with three coding entries:

```json
"code": {
  "coding": [
    {
      "system": "http://snomed.info/sct",
      "code": "1197209002",
      "display": "Castration-sensitive prostate cancer"
    },
    {
      "system": "http://hl7.org/fhir/sid/icd-10-cm",
      "code": "Z19.1",
      "display": "Hormone sensitive malignancy status"
    },
    {
      "system": "http://hl7.org/fhir/sid/icd-10-cm",
      "code": "R97.21",
      "display": "Rising PSA following treatment for malignant neoplasm of prostate"
    }
  ],
  "text": "Castration-Sensitive Prostate Cancer with Biochemical Relapse"
}
```

## Castration-Resistant Example

For metastatic castration-resistant cases:

```csharp
var mcrpcCondition = builder
    .WithFocus("Condition/prostate-cancer-metastatic")
    .WithPatient("Patient/patient-789")
    .WithDevice("Device/ai-classifier")
    .WithHSDMResult(HsdmAssessmentConditionBuilder.HsdmResults.MetastaticCastrationResistant)
    .AddFactEvidence(resistanceFacts)
    .WithSummary("Patient has progressed to castration-resistant disease with continued PSA rise despite ADT")
    .Build();
```

## API Reference

### Required Methods

These methods **must** be called before `Build()`:

- `WithFocus(ResourceReference)` - Reference to existing prostate cancer Condition
- `WithPatient(ResourceReference)` - Patient reference
- `WithDevice(ResourceReference)` - AI device performing assessment
- `WithHSDMResult(string)` - One of the three supported result types
- `AddFactEvidence(Fact[])` - Clinical facts supporting the assessment
- `WithSummary(string)` - Summary note explaining the assessment

### Optional Methods

- `WithInferenceId(string)` - Custom inference ID (auto-generated if not provided)
- `WithCriteria(string, string, string)` - Assessment criteria details
- `AddEvidence(ResourceReference)` - Additional evidence references
- `WithConfidence(float)` - AI confidence score (0.0-1.0)
- `WithEffectiveDate(DateTime)` - Assessment date (defaults to current time)

### Validation

The builder performs strict validation:

- **Focus** must reference a Condition resource (starts with "Condition/")
- **HSDM Result** must be one of the three supported values
- **Facts** array cannot be empty
- **Summary** cannot be null or empty
- **Confidence** must be between 0.0 and 1.0 if provided

### Error Handling

```csharp
try
{
    var condition = builder
        .WithFocus("InvalidReference") // Will throw ArgumentException
        .Build();
}
catch (ArgumentException ex)
{
    // Handle invalid reference format
}
catch (InvalidOperationException ex)
{
    // Handle missing required fields
}
```

## Clinical Fact Extensions

The builder automatically converts `Fact` objects into FHIR extensions using the URL
`https://thirdopinion.io/clinical-fact`. Each fact becomes a complex extension with sub-extensions for all fact
properties.

## Integration Notes

- The builder extends `AiResourceBuilderBase<Condition>` for consistent AI resource patterns
- All conditions receive the AIAST (AI Assisted) security label
- Generated resources are compatible with FHIR R4
- The builder follows the fluent interface pattern for method chaining