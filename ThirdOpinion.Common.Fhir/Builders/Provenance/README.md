# AI Provenance Builder

The `AiProvenanceBuilder` creates FHIR Provenance resources that track the audit trail, lineage, and accountability for
AI-generated clinical resources and inferences.

## Overview

The AI Provenance builder generates FHIR R4 Provenance resources that provide comprehensive audit trails for
AI-generated clinical content. These resources establish accountability, traceability, and lineage for AI inferences,
supporting regulatory compliance and clinical governance requirements.

## Purpose

AI Provenance resources serve critical functions in clinical AI systems:

- **Audit Trails**: Track who, what, when, where, and why for AI-generated content
- **Accountability**: Establish responsibility chains for AI decisions
- **Regulatory Compliance**: Support FDA, EU MDR, and other regulatory requirements
- **Quality Assurance**: Enable monitoring and validation of AI system behavior
- **Legal Documentation**: Provide evidence for medical-legal purposes
- **Workflow Transparency**: Make AI decision processes visible to clinicians

## Required Dependencies

```csharp
using Hl7.Fhir.Model;
using ThirdOpinion.Common.Fhir.Builders.Provenance;
using ThirdOpinion.Common.Fhir.Configuration;
using ThirdOpinion.Common.Fhir.Helpers;
```

## Basic Usage Example

```csharp
// Create configuration
var config = AiInferenceConfiguration.CreateDefault();

// Build AI Provenance for an HSDM Assessment
var provenance = new AiProvenanceBuilder(config)
    .WithInferenceId("provenance-001")
    .WithTarget("Condition/hsdm-assessment-123", "HSDM Assessment Condition")
    .WithActivity(AiProvenanceBuilder.Activities.AiInference, "AI-based clinical inference")
    .WithAgent("Device/ai-hsdm-classifier-v2", "HSDM AI Classifier v2.1",
               AiProvenanceBuilder.AgentRoles.Performer)
    .WithAgent("Organization/thirdopinion-io", "ThirdOpinion.io",
               AiProvenanceBuilder.AgentRoles.Author)
    .WithAgent("Person/clinician-456", "Dr. Sarah Johnson",
               AiProvenanceBuilder.AgentRoles.Verifier)
    .WithOccurredPeriod(DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow)
    .WithLocation("Location/hospital-system-001", "Academic Medical Center")
    .WithReason("Clinical decision support for prostate cancer treatment planning")
    .AddSourceEntity("DocumentReference/pathology-report-001", "Source pathology report",
                     AiProvenanceBuilder.EntityRoles.Source)
    .AddSourceEntity("Observation/psa-measurements", "PSA trend data",
                     AiProvenanceBuilder.EntityRoles.Source)
    .WithConfidence(0.94f)
    .AddNote("Inference generated using validated AI model with clinical oversight")
    .Build();
```

## Advanced Example with Complex Workflow

```csharp
// Multi-step AI workflow with comprehensive provenance
var comprehensiveProvenance = new AiProvenanceBuilder(config)
    .WithTarget("Condition/hsdm-assessment-456")
    .WithActivity(AiProvenanceBuilder.Activities.AiWorkflow, "Multi-step AI inference pipeline")

    // AI system agents
    .WithAgent("Device/ocr-engine", "OCR Text Extraction Engine",
               AiProvenanceBuilder.AgentRoles.Performer)
    .WithAgent("Device/fact-extractor", "Clinical Fact Extraction AI",
               AiProvenanceBuilder.AgentRoles.Performer)
    .WithAgent("Device/hsdm-classifier", "HSDM Classification AI",
               AiProvenanceBuilder.AgentRoles.Performer)

    // Human oversight agents
    .WithAgent("Person/radiologist-123", "Dr. Michael Chen, MD",
               AiProvenanceBuilder.AgentRoles.Verifier)
    .WithAgent("Person/oncologist-789", "Dr. Lisa Rodriguez, MD",
               AiProvenanceBuilder.AgentRoles.Reviewer)

    // Organizational accountability
    .WithAgent("Organization/thirdopinion-io", "ThirdOpinion.io",
               AiProvenanceBuilder.AgentRoles.Author)
    .WithAgent("Organization/hospital-system", "University Medical Center",
               AiProvenanceBuilder.AgentRoles.Custodian)

    // Temporal tracking
    .WithOccurredPeriod(DateTime.UtcNow.AddHours(-2), DateTime.UtcNow)
    .WithRecordedTime(DateTime.UtcNow)

    // Location and context
    .WithLocation("Location/radiology-dept", "Radiology Department")
    .WithReason("Automated clinical decision support workflow for oncology case review")

    // Input sources
    .AddSourceEntity("DocumentReference/ct-scan-report", "CT scan interpretation",
                     AiProvenanceBuilder.EntityRoles.Source)
    .AddSourceEntity("DocumentReference/pathology-slides", "Digital pathology images",
                     AiProvenanceBuilder.EntityRoles.Source)
    .AddSourceEntity("Observation/lab-results", "Laboratory test results",
                     AiProvenanceBuilder.EntityRoles.Source)

    // Intermediate outputs
    .AddSourceEntity("DocumentReference/ocr-text", "OCR extracted text",
                     AiProvenanceBuilder.EntityRoles.Intermediary)
    .AddSourceEntity("DocumentReference/extracted-facts", "Clinical facts",
                     AiProvenanceBuilder.EntityRoles.Intermediary)

    // Confidence and quality
    .WithConfidence(0.91f)
    .WithValidationStatus("Clinically reviewed and approved")

    // Documentation
    .AddNote("Multi-step AI workflow with human validation at each stage")
    .AddNote("All AI models FDA-cleared for clinical decision support")
    .AddNote("Results reviewed by board-certified specialists")

    .Build();
```

## Example JSON Output

The builder generates FHIR Provenance resources like this:

```json
{
  "resourceType": "Provenance",
  "id": "provenance-001",
  "meta": {
    "security": [
      {
        "system": "http://terminology.hl7.org/CodeSystem/v3-ActCode",
        "code": "AIAST",
        "display": "AI Assisted"
      }
    ]
  },
  "target": [
    {
      "reference": "Condition/hsdm-assessment-123",
      "display": "HSDM Assessment Condition"
    }
  ],
  "occurredPeriod": {
    "start": "2024-01-15T10:25:00Z",
    "end": "2024-01-15T10:30:00Z"
  },
  "recorded": "2024-01-15T10:30:15Z",
  "activity": {
    "coding": [
      {
        "system": "http://terminology.hl7.org/CodeSystem/v3-DataOperation",
        "code": "CREATE",
        "display": "create"
      },
      {
        "system": "http://thirdopinion.ai/fhir/CodeSystem/ai-activities",
        "code": "ai-inference",
        "display": "AI-based clinical inference"
      }
    ],
    "text": "AI-based clinical inference"
  },
  "location": {
    "reference": "Location/hospital-system-001",
    "display": "Academic Medical Center"
  },
  "reason": [
    {
      "text": "Clinical decision support for prostate cancer treatment planning"
    }
  ],
  "agent": [
    {
      "type": {
        "coding": [
          {
            "system": "http://terminology.hl7.org/CodeSystem/provenance-participant-type",
            "code": "performer",
            "display": "Performer"
          }
        ]
      },
      "who": {
        "reference": "Device/ai-hsdm-classifier-v2",
        "display": "HSDM AI Classifier v2.1"
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
        "reference": "Organization/thirdopinion-io",
        "display": "ThirdOpinion.io"
      }
    },
    {
      "type": {
        "coding": [
          {
            "system": "http://terminology.hl7.org/CodeSystem/provenance-participant-type",
            "code": "verifier",
            "display": "Verifier"
          }
        ]
      },
      "who": {
        "reference": "Person/clinician-456",
        "display": "Dr. Sarah Johnson"
      }
    }
  ],
  "entity": [
    {
      "role": "source",
      "what": {
        "reference": "DocumentReference/pathology-report-001",
        "display": "Source pathology report"
      }
    },
    {
      "role": "source",
      "what": {
        "reference": "Observation/psa-measurements",
        "display": "PSA trend data"
      }
    }
  ],
  "extension": [
    {
      "url": "http://thirdopinion.ai/fhir/StructureDefinition/confidence",
      "valueDecimal": 0.94
    },
    {
      "url": "http://thirdopinion.ai/fhir/StructureDefinition/validation-status",
      "valueString": "Clinically validated"
    }
  ]
}
```

## API Reference

### Required Methods

These methods **must** be called before `Build()`:

- `WithTarget(ResourceReference)` - Resource that this provenance describes
- `WithActivity(string, string)` - Activity that was performed
- `WithAgent(ResourceReference, string, string)` - At least one agent must be specified
- `WithOccurredPeriod(DateTime, DateTime)` - When the activity occurred

### Optional Methods

#### Basic Information

- `WithInferenceId(string)` - Custom provenance ID (auto-generated if not provided)
- `WithRecordedTime(DateTime)` - When provenance was recorded (defaults to now)
- `WithLocation(ResourceReference)` - Where the activity occurred
- `WithReason(string)` - Why the activity was performed

#### Agents and Participants

- `WithAgent(ResourceReference, string, string)` - Add participant (device, person, organization)
- Multiple agents can be added with different roles

#### Source Data and Entities

- `AddSourceEntity(ResourceReference, string, string)` - Add source data or intermediate results
- Support for source, intermediary, and derived entity roles

#### Quality and Validation

- `WithConfidence(float)` - Overall confidence in the provenance (0.0-1.0)
- `WithValidationStatus(string)` - Validation or review status
- `AddNote(string)` - Add explanatory notes

### Predefined Constants

#### Activity Types

```csharp
public static class Activities
{
    public const string AiInference = "ai-inference";
    public const string AiWorkflow = "ai-workflow";
    public const string AiTraining = "ai-training";
    public const string AiValidation = "ai-validation";
    public const string DataProcessing = "data-processing";
    public const string ClinicalReview = "clinical-review";
}
```

#### Agent Roles

```csharp
public static class AgentRoles
{
    public const string Performer = "performer";      // Executed the activity
    public const string Author = "author";            // Created or authored
    public const string Verifier = "verifier";        // Verified or validated
    public const string Reviewer = "reviewer";        // Reviewed for quality
    public const string Custodian = "custodian";      // Responsible for custody
    public const string Informant = "informant";      // Provided information
    public const string Assembler = "assembler";      // Assembled information
}
```

#### Entity Roles

```csharp
public static class EntityRoles
{
    public const string Source = "source";            // Input data
    public const string Intermediary = "intermediary"; // Intermediate processing result
    public const string Derived = "derived";          // Final derived result
    public const string Revision = "revision";        // Revision of existing data
    public const string Quotation = "quotation";      // Quote from source
}
```

## Validation

The builder performs strict validation:

- **Target resource** must be provided and valid
- **Activity** must be specified with valid coding
- **At least one agent** must be specified
- **Occurred period** start must be before end time
- **Confidence** must be between 0.0 and 1.0 if provided
- **Agent roles** must be valid provenance participant types
- **Entity roles** must be valid provenance entity roles

### Error Handling

```csharp
try
{
    var provenance = builder.Build();
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
catch (ArgumentOutOfRangeException ex)
{
    // Handle invalid confidence scores or time ranges
    _logger.LogError("Value out of range: {Message}", ex.Message);
}
```

## Clinical Workflow Integration

### Automatic Provenance Generation

```csharp
public class ClinicalInferenceService
{
    public async Task<(Condition condition, Provenance provenance)> CreateHsdmAssessmentAsync(
        string patientId,
        Device aiDevice,
        Person clinician,
        DocumentReference[] sourceDocuments)
    {
        var startTime = DateTime.UtcNow;

        // Create HSDM assessment
        var condition = new HsdmAssessmentConditionBuilder(_config)
            .WithPatient(patientId)
            .WithDevice(aiDevice.AsReference())
            // ... other configuration
            .Build();

        var endTime = DateTime.UtcNow;

        // Create comprehensive provenance
        var provenance = new AiProvenanceBuilder(_config)
            .WithTarget(condition.AsReference())
            .WithActivity(AiProvenanceBuilder.Activities.AiInference,
                         "HSDM classification using AI")
            .WithAgent(aiDevice.AsReference(), aiDevice.DeviceName?.FirstOrDefault()?.Name,
                      AiProvenanceBuilder.AgentRoles.Performer)
            .WithAgent(clinician.AsReference(), clinician.Name?.ToString(),
                      AiProvenanceBuilder.AgentRoles.Verifier)
            .WithOccurredPeriod(startTime, endTime)
            .WithReason("Clinical decision support for hormone sensitivity classification");

        // Add all source documents
        foreach (var doc in sourceDocuments)
        {
            provenance.AddSourceEntity(doc.AsReference(), doc.Description?.ToString(),
                                     AiProvenanceBuilder.EntityRoles.Source);
        }

        return (condition, provenance.Build());
    }
}
```

### Audit Trail Queries

```csharp
public class ProvenanceQueryService
{
    public async Task<List<Provenance>> GetAuditTrailAsync(string resourceId)
    {
        // Query for all provenance records targeting this resource
        var searchParams = new SearchParams()
            .Add("target", resourceId)
            .Sort("recorded", SortOrder.Descending);

        var bundle = await _fhirClient.SearchAsync<Provenance>(searchParams);
        return bundle.Entry.Select(e => e.Resource as Provenance).ToList();
    }

    public async Task<List<Provenance>> GetAiInferencesAsync(DateTime since)
    {
        // Query for AI-generated content since specified date
        var searchParams = new SearchParams()
            .Add("recorded", $"ge{since:yyyy-MM-dd}")
            .Add("activity", "http://thirdopinion.ai/fhir/CodeSystem/ai-activities|ai-inference");

        var bundle = await _fhirClient.SearchAsync<Provenance>(searchParams);
        return bundle.Entry.Select(e => e.Resource as Provenance).ToList();
    }
}
```

## Regulatory Compliance Examples

### FDA Audit Documentation

```csharp
var fdaAuditProvenance = new AiProvenanceBuilder(config)
    .WithTarget(clinicalCondition.AsReference())
    .WithActivity(AiProvenanceBuilder.Activities.AiInference,
                 "FDA-cleared AI clinical decision support")
    .WithAgent(fdaClearedDevice.AsReference(), "FDA-cleared AI Device v2.1",
              AiProvenanceBuilder.AgentRoles.Performer)
    .WithAgent(qualifiedClinician.AsReference(), "Board-certified specialist",
              AiProvenanceBuilder.AgentRoles.Verifier)
    .WithAgent(hospitalOrganization.AsReference(), "Accredited medical facility",
              AiProvenanceBuilder.AgentRoles.Custodian)
    .WithReason("Clinical decision support per FDA 510(k) clearance K243567")
    .WithValidationStatus("Clinically reviewed and approved by qualified physician")
    .AddNote("AI device used within FDA-cleared intended use parameters")
    .AddNote("Clinical oversight maintained per institutional policy")
    .Build();
```

### EU MDR Compliance

```csharp
var mdrComplianceProvenance = new AiProvenanceBuilder(config)
    .WithTarget(aiGeneratedAssessment.AsReference())
    .WithActivity(AiProvenanceBuilder.Activities.AiInference,
                 "CE-marked medical device software inference")
    .WithAgent(ceClearedDevice.AsReference(), "CE-marked AI System Class IIa",
              AiProvenanceBuilder.AgentRoles.Performer)
    .WithAgent(euQualifiedPerson.AsReference(), "EU Qualified Person",
              AiProvenanceBuilder.AgentRoles.Verifier)
    .WithReason("Clinical decision support per EU MDR Article 22")
    .WithValidationStatus("Validated per EU MDR post-market surveillance")
    .AddNote("Device used within CE marking intended purpose")
    .AddNote("Post-market clinical follow-up maintained")
    .Build();
```

## Performance and Scalability

### Batch Provenance Creation

```csharp
public class BatchProvenanceService
{
    public async Task<List<Provenance>> CreateBatchProvenanceAsync(
        List<Resource> aiGeneratedResources,
        Device aiDevice,
        Person clinician)
    {
        var provenanceRecords = new List<Provenance>();
        var batchStartTime = DateTime.UtcNow;

        foreach (var resource in aiGeneratedResources)
        {
            var provenance = new AiProvenanceBuilder(_config)
                .WithTarget(resource.AsReference())
                .WithActivity(AiProvenanceBuilder.Activities.AiWorkflow, "Batch AI processing")
                .WithAgent(aiDevice.AsReference(), aiDevice.DeviceName?.FirstOrDefault()?.Name,
                          AiProvenanceBuilder.AgentRoles.Performer)
                .WithAgent(clinician.AsReference(), clinician.Name?.ToString(),
                          AiProvenanceBuilder.AgentRoles.Reviewer)
                .WithOccurredPeriod(batchStartTime, DateTime.UtcNow)
                .WithReason($"Batch processing of {aiGeneratedResources.Count} clinical resources")
                .Build();

            provenanceRecords.Add(provenance);
        }

        // Store all provenance records
        await _fhirClient.TransactionAsync(provenanceRecords);
        return provenanceRecords;
    }
}
```

## Best Practices

### Comprehensive Documentation

1. **Always record AI system details** including version and capabilities
2. **Include human oversight** when clinicians review AI outputs
3. **Document all input sources** that influenced the AI decision
4. **Track temporal information** accurately for audit purposes

### Regulatory Preparedness

1. **Maintain detailed audit trails** for all AI-generated content
2. **Document validation workflows** and human review processes
3. **Track device compliance status** and regulatory clearances
4. **Record post-market surveillance** activities and outcomes

### Performance Optimization

1. **Batch provenance creation** for high-volume AI workflows
2. **Use appropriate detail levels** based on regulatory requirements
3. **Implement efficient querying** for audit trail retrieval
4. **Archive old provenance** records per data retention policies

### Clinical Integration

1. **Make provenance accessible** to clinicians for transparency
2. **Integrate with EHR systems** for seamless workflow
3. **Provide audit trail views** for quality assurance
4. **Enable compliance reporting** for regulatory submissions

## Integration Notes

- The builder extends `AiResourceBuilderBase<Provenance>` for consistent AI resource patterns
- All provenance records receive the AIAST (AI Assisted) security label automatically
- Generated resources are compatible with FHIR R4 and US Core Provenance profiles
- The builder follows the fluent interface pattern for method chaining
- Provenance resources support complex audit trail queries and regulatory compliance reporting