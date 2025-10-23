# AI Device Builder

The `AiDeviceBuilder` creates FHIR Device resources representing AI/ML systems, algorithms, and software devices used in clinical inference and assessment workflows.

## Overview

The AI Device builder generates FHIR R4 Device resources that represent artificial intelligence systems, machine learning models, and clinical decision support software. These devices are referenced by other FHIR resources (Observations, Conditions, DocumentReferences) to indicate which AI system performed specific clinical inferences.

## Purpose

AI Device resources serve several critical functions:
- **Attribution**: Identify which AI system generated clinical inferences
- **Traceability**: Enable audit trails for AI-generated content
- **Version Control**: Track different versions of AI models and algorithms
- **Regulation Compliance**: Support FDA and other regulatory requirements
- **Quality Assurance**: Enable monitoring of AI system performance

## Required Dependencies

```csharp
using Hl7.Fhir.Model;
using ThirdOpinion.Common.Fhir.Builders.Devices;
using ThirdOpinion.Common.Fhir.Configuration;
using ThirdOpinion.Common.Fhir.Helpers;
```

## Basic Usage Example

```csharp
// Create configuration
var config = AiInferenceConfiguration.CreateDefault();

// Build AI Device
var aiDevice = new AiDeviceBuilder(config)
    .WithInferenceId("ai-device-001")
    .WithName("Prostate Cancer AI Classifier")
    .WithVersion("2.1.0")
    .WithManufacturer("ThirdOpinion.io", "https://thirdopinion.io")
    .WithModelType("neural-network")
    .WithDescription("Deep learning model for prostate cancer hormone sensitivity classification")
    .WithAlgorithmName("HSDM-CNN-v2")
    .WithTrainingData("10,000 pathology reports from academic medical centers")
    .WithValidationAccuracy(0.94f)
    .WithRegulationStatus("FDA-cleared", "510(k) clearance for clinical decision support")
    .AddCapability("HSDM Assessment", "Hormone Sensitivity Diagnosis Modifier classification")
    .AddCapability("PSA Progression", "PSA progression analysis using PCWG3 criteria")
    .WithLastCalibration(DateTime.UtcNow.AddDays(-30))
    .WithMaintenanceSchedule("Monthly model updates and recalibration")
    .Build();
```

## Advanced Example with Multiple Capabilities

```csharp
var comprehensiveAiDevice = new AiDeviceBuilder(config)
    .WithName("ThirdOpinion Clinical AI Suite")
    .WithVersion("3.2.1")
    .WithManufacturer("ThirdOpinion.io")
    .WithModelType("ensemble")
    .WithDescription("Comprehensive AI system for prostate cancer clinical decision support")
    .WithAlgorithmName("Ensemble-HSDM-PSA-RECIST")

    // Multiple capabilities
    .AddCapability("HSDM Assessment", "Castration sensitivity classification")
    .AddCapability("PSA Progression", "Biochemical progression detection")
    .AddCapability("RECIST Assessment", "Radiographic progression evaluation")
    .AddCapability("ADT Status", "Androgen deprivation therapy status inference")

    // Technical specifications
    .WithTrainingData("25,000 patient records, 50,000 pathology reports, 75,000 lab results")
    .WithValidationAccuracy(0.96f)
    .WithProcessingSpeed("< 2 seconds per patient assessment")

    // Regulatory and compliance
    .WithRegulationStatus("FDA-cleared", "Class II medical device software")
    .WithCertification("ISO 13485", "Quality management for medical devices")
    .WithCertification("ISO 14155", "Clinical investigation of medical devices")

    // Operational details
    .WithLastCalibration(DateTime.UtcNow.AddDays(-15))
    .WithMaintenanceSchedule("Bi-weekly model updates, monthly full recalibration")
    .WithSupportContact("support@thirdopinion.io", "24/7 technical support")

    .Build();
```

## Example JSON Output

The builder generates FHIR Device resources like this:

```json
{
  "resourceType": "Device",
  "id": "ai-device-001",
  "meta": {
    "security": [
      {
        "system": "http://terminology.hl7.org/CodeSystem/v3-ActCode",
        "code": "AIAST",
        "display": "AI Assisted"
      }
    ]
  },
  "identifier": [
    {
      "use": "usual",
      "system": "https://thirdopinion.io/device-registry",
      "value": "ai-device-001"
    }
  ],
  "status": "active",
  "type": {
    "coding": [
      {
        "system": "http://snomed.info/sct",
        "code": "706689003",
        "display": "Artificial intelligence algorithm"
      }
    ],
    "text": "AI/ML Clinical Decision Support System"
  },
  "manufacturer": "ThirdOpinion.io",
  "deviceName": [
    {
      "name": "Prostate Cancer AI Classifier",
      "type": "manufacturer-name"
    }
  ],
  "version": [
    {
      "type": {
        "coding": [
          {
            "system": "http://terminology.hl7.org/CodeSystem/device-nametype",
            "code": "model-name",
            "display": "Model name"
          }
        ]
      },
      "value": "2.1.0"
    }
  ],
  "note": [
    {
      "time": "2024-01-15T10:30:00Z",
      "text": "Deep learning model for prostate cancer hormone sensitivity classification"
    }
  ],
  "extension": [
    {
      "url": "http://thirdopinion.ai/fhir/StructureDefinition/ai-model-type",
      "valueString": "neural-network"
    },
    {
      "url": "http://thirdopinion.ai/fhir/StructureDefinition/algorithm-name",
      "valueString": "HSDM-CNN-v2"
    },
    {
      "url": "http://thirdopinion.ai/fhir/StructureDefinition/training-data",
      "valueString": "10,000 pathology reports from academic medical centers"
    },
    {
      "url": "http://thirdopinion.ai/fhir/StructureDefinition/validation-accuracy",
      "valueDecimal": 0.94
    },
    {
      "url": "http://thirdopinion.ai/fhir/StructureDefinition/device-capability",
      "extension": [
        {
          "url": "name",
          "valueString": "HSDM Assessment"
        },
        {
          "url": "description",
          "valueString": "Hormone Sensitivity Diagnosis Modifier classification"
        }
      ]
    },
    {
      "url": "http://thirdopinion.ai/fhir/StructureDefinition/regulation-status",
      "extension": [
        {
          "url": "status",
          "valueString": "FDA-cleared"
        },
        {
          "url": "description",
          "valueString": "510(k) clearance for clinical decision support"
        }
      ]
    },
    {
      "url": "http://thirdopinion.ai/fhir/StructureDefinition/last-calibration",
      "valueDateTime": "2023-12-15T10:30:00Z"
    },
    {
      "url": "http://thirdopinion.ai/fhir/StructureDefinition/maintenance-schedule",
      "valueString": "Monthly model updates and recalibration"
    }
  ]
}
```

## API Reference

### Required Methods

These methods **must** be called before `Build()`:

- `WithName(string)` - AI system/model name
- `WithVersion(string)` - Model version (semantic versioning recommended)
- `WithManufacturer(string)` - Organization that developed the AI system

### Optional Methods

#### Basic Information
- `WithInferenceId(string)` - Custom device ID (auto-generated if not provided)
- `WithDescription(string)` - Detailed description of the AI system
- `WithModelType(string)` - Type of ML model (neural-network, ensemble, rule-based, etc.)
- `WithAlgorithmName(string)` - Specific algorithm or architecture name

#### Technical Specifications
- `WithTrainingData(string)` - Description of training dataset
- `WithValidationAccuracy(float)` - Model validation accuracy (0.0-1.0)
- `WithProcessingSpeed(string)` - Performance characteristics
- `AddCapability(string, string)` - Add specific clinical capabilities

#### Regulatory and Compliance
- `WithRegulationStatus(string, string)` - Regulatory clearance status and details
- `WithCertification(string, string)` - Quality certifications
- `WithManufacturer(string, string)` - Manufacturer with optional URL

#### Operational Details
- `WithLastCalibration(DateTime)` - Last calibration/validation date
- `WithMaintenanceSchedule(string)` - Maintenance and update schedule
- `WithSupportContact(string, string)` - Support contact information

### Device Types and Standards

#### Model Types
- `"neural-network"` - Deep learning, CNN, RNN, transformer models
- `"ensemble"` - Combination of multiple models
- `"rule-based"` - Expert system with predefined rules
- `"decision-tree"` - Tree-based algorithms (Random Forest, XGBoost)
- `"statistical"` - Classical statistical models
- `"hybrid"` - Combination of AI and traditional approaches

#### Common Capabilities
- `"HSDM Assessment"` - Hormone Sensitivity Diagnosis Modifier
- `"PSA Progression"` - PSA biochemical progression
- `"RECIST Assessment"` - Radiographic progression per RECIST 1.1
- `"ADT Status"` - Androgen deprivation therapy status
- `"Risk Stratification"` - Patient risk classification
- `"Treatment Response"` - Therapy response prediction

## Validation

The builder performs strict validation:

- **Name** cannot be null or empty
- **Version** must follow semantic versioning (recommended) or be non-empty
- **Manufacturer** cannot be null or empty
- **Validation accuracy** must be between 0.0 and 1.0 if provided
- **Capability names** cannot be duplicate within the same device
- **Contact information** must be valid email format if provided

### Error Handling

```csharp
try
{
    var device = builder.Build();
}
catch (InvalidOperationException ex)
{
    // Handle missing required fields
    _logger.LogError("Missing required field: {Message}", ex.Message);
}
catch (ArgumentException ex)
{
    // Handle invalid parameter values (e.g., invalid accuracy score)
    _logger.LogError("Invalid parameter: {Message}", ex.Message);
}
catch (FormatException ex)
{
    // Handle invalid email format in contact information
    _logger.LogError("Invalid format: {Message}", ex.Message);
}
```

## AWS Integration Examples

### Lambda Function Registration

```csharp
public class DeviceRegistrationHandler
{
    private readonly AiInferenceConfiguration _config;

    public async Task<APIGatewayProxyResponse> RegisterDeviceAsync(APIGatewayProxyRequest request)
    {
        var deviceInfo = JsonSerializer.Deserialize<DeviceRegistrationRequest>(request.Body);

        var aiDevice = new AiDeviceBuilder(_config)
            .WithName(deviceInfo.Name)
            .WithVersion(deviceInfo.Version)
            .WithManufacturer("ThirdOpinion.io")
            .WithModelType(deviceInfo.ModelType)
            .WithDescription(deviceInfo.Description)
            .WithValidationAccuracy(deviceInfo.Accuracy)
            .WithRegulationStatus(deviceInfo.RegulatoryStatus, deviceInfo.RegulatoryDetails)
            .Build();

        // Store device in FHIR server
        await _fhirClient.CreateAsync(aiDevice);

        return new APIGatewayProxyResponse
        {
            StatusCode = 201,
            Body = JsonSerializer.Serialize(new { DeviceId = aiDevice.Id })
        };
    }
}
```

### Model Version Management

```csharp
public class ModelVersionManager
{
    public async Task<Device> CreateNewVersionAsync(string baseDeviceId, string newVersion, float newAccuracy)
    {
        // Get base device
        var baseDevice = await _fhirClient.ReadAsync<Device>(baseDeviceId);

        // Create new version
        var updatedDevice = new AiDeviceBuilder(_config)
            .WithName(baseDevice.DeviceName.First().Name)
            .WithVersion(newVersion)
            .WithManufacturer(baseDevice.Manufacturer)
            .WithValidationAccuracy(newAccuracy)
            .WithLastCalibration(DateTime.UtcNow)
            .WithDescription($"Updated model with improved accuracy: {newAccuracy:P1}")
            .Build();

        return updatedDevice;
    }
}
```

## Clinical Workflow Integration

### Device Selection for Inference

```csharp
public class ClinicalInferenceService
{
    public async Task<Observation> PerformHsdmAssessmentAsync(string patientId, Device aiDevice)
    {
        // Verify device capabilities
        var hasHsdmCapability = aiDevice.Extension
            .Where(ext => ext.Url == "http://thirdopinion.ai/fhir/StructureDefinition/device-capability")
            .Any(ext => ext.Extension
                .Any(subExt => subExt.Url == "name" &&
                               ((FhirString)subExt.Value).Value == "HSDM Assessment"));

        if (!hasHsdmCapability)
        {
            throw new InvalidOperationException("Device does not support HSDM Assessment capability");
        }

        // Create HSDM assessment using this device
        var observation = new HsdmAssessmentConditionBuilder(_config)
            .WithPatient(patientId)
            .WithDevice(aiDevice.AsReference())
            .WithHSDMResult(HsdmAssessmentConditionBuilder.HsdmResults.MetastaticCastrationSensitive)
            // ... other configuration
            .Build();

        return observation;
    }
}
```

### Multi-Device Ensemble

```csharp
public class EnsembleInferenceService
{
    public async Task<float> GetEnsembleConfidenceAsync(List<Device> aiDevices, string assessment)
    {
        var deviceAccuracies = aiDevices.Select(device =>
        {
            var accuracyExt = device.Extension
                .FirstOrDefault(ext => ext.Url == "http://thirdopinion.ai/fhir/StructureDefinition/validation-accuracy");

            return accuracyExt != null ? ((FhirDecimal)accuracyExt.Value).Value ?? 0.5m : 0.5m;
        }).ToList();

        // Weighted average based on individual device accuracies
        var weightedConfidence = deviceAccuracies.Average();
        return (float)weightedConfidence;
    }
}
```

## Regulatory Compliance

### FDA Documentation

For FDA-regulated AI/ML devices, include comprehensive documentation:

```csharp
var fdaCompliantDevice = new AiDeviceBuilder(config)
    .WithName("Clinical Decision Support AI")
    .WithVersion("1.0.0")
    .WithManufacturer("ThirdOpinion.io", "https://thirdopinion.io")
    .WithRegulationStatus("FDA-cleared", "510(k) K243567 - Class II Medical Device Software")
    .WithCertification("ISO 13485", "Quality Management Systems for Medical Devices")
    .WithCertification("IEC 62304", "Medical Device Software Lifecycle Processes")
    .WithDescription("AI/ML-based clinical decision support for prostate cancer management")
    .WithTrainingData("IRB-approved dataset: 15,000 de-identified patient records from 5 academic medical centers")
    .WithValidationAccuracy(0.94f)
    .WithLastCalibration(DateTime.UtcNow.AddDays(-30))
    .WithMaintenanceSchedule("Monthly algorithm updates with performance monitoring")
    .WithSupportContact("regulatory@thirdopinion.io", "Regulatory and clinical support")
    .Build();
```

### EU MDR Compliance

For European Medical Device Regulation compliance:

```csharp
var mdrCompliantDevice = new AiDeviceBuilder(config)
    .WithName("Prostate Cancer AI Classifier")
    .WithVersion("2.1.0")
    .WithManufacturer("ThirdOpinion Europe GmbH")
    .WithRegulationStatus("CE-marked", "MDR Class IIa - Notified Body 1234")
    .WithCertification("ISO 13485", "Quality Management Systems")
    .WithCertification("ISO 14971", "Risk Management for Medical Devices")
    .WithDescription("Software as Medical Device (SaMD) for clinical decision support")
    .WithValidationAccuracy(0.96f)
    .WithSupportContact("compliance@thirdopinion.eu", "EU regulatory compliance")
    .Build();
```

## Best Practices

### Version Management
1. **Use semantic versioning** (major.minor.patch) for model versions
2. **Document breaking changes** between major versions
3. **Track model performance** across versions for comparison
4. **Maintain backward compatibility** when possible

### Capability Documentation
1. **Be specific** about what the AI system can and cannot do
2. **Include limitations** and edge cases in descriptions
3. **Reference clinical guidelines** that the AI implements
4. **Document training data characteristics** and potential biases

### Regulatory Preparation
1. **Keep detailed records** of model development and validation
2. **Document data sources** and patient population characteristics
3. **Track model performance** in production environments
4. **Maintain audit trails** for model updates and changes

### Clinical Integration
1. **Verify device capabilities** before using in clinical workflows
2. **Check calibration dates** to ensure models are current
3. **Monitor performance** and report issues to manufacturers
4. **Follow institution policies** for AI/ML device usage

## Integration Notes

- The builder extends `AiResourceBuilderBase<Device>` for consistent AI resource patterns
- All devices receive the AIAST (AI Assisted) security label automatically
- Generated resources are compatible with FHIR R4 and follow HL7 Device resource guidelines
- The builder follows the fluent interface pattern for method chaining
- Device resources can be referenced by other FHIR resources for attribution and traceability