# Base Infrastructure

Core infrastructure classes that provide the foundation for all FHIR resource builders in the ThirdOpinion.Common.Fhir library.

## AiResourceBuilderBase&lt;T&gt;

Abstract base class providing common functionality for all AI resource builders.

### Purpose
- Standardizes AI metadata application (AIAST security labels, inference IDs)
- Provides fluent interface patterns
- Handles common validation logic
- Manages derivedFrom relationships

### Generic Type Parameter
- `T` - Constrained to `Hl7.Fhir.Model.Resource`

### Core Properties
```csharp
protected string? InferenceId { get; set; }
protected string? CriteriaId { get; set; }
protected string? CriteriaDisplay { get; set; }
protected string? CriteriaSystem { get; set; }
protected List<ResourceReference> DerivedFromReferences { get; }
protected AiInferenceConfiguration Configuration { get; }
```

### Common Methods

#### WithInferenceId(string id)
Sets the inference ID for this resource. If not provided, a unique ID is auto-generated.

```csharp
var builder = new SomeBuilder(config)
    .WithInferenceId("custom-inference-123")
    .Build();
```

#### WithCriteria(string id, string display, string? system)
Sets assessment criteria information used by the AI system.

```csharp
var builder = new SomeBuilder(config)
    .WithCriteria("PSA-PROG-v2.1", "PSA Progression Criteria v2.1",
                  "https://thirdopinion.io/criteria")
    .Build();
```

#### AddDerivedFrom(ResourceReference reference)
Adds resources that this inference was derived from.

```csharp
var builder = new SomeBuilder(config)
    .AddDerivedFrom("Observation/psa-123", "PSA measurement")
    .AddDerivedFrom("DocumentReference/report-456", "Radiology report")
    .Build();
```

### Automatic Features

#### AIAST Security Label
All resources automatically receive the AI Assisted security label:
```json
{
  "meta": {
    "security": [
      {
        "system": "http://terminology.hl7.org/CodeSystem/v3-ActCode",
        "code": "AIAST",
        "display": "AI Assisted"
      }
    ]
  }
}
```

#### Inference ID Generation
If no inference ID is provided, one is automatically generated using the pattern:
```
to.io-{GUID}
```

#### Validation Framework
The base class provides:
- **EnsureInferenceId()** - Ensures an inference ID exists
- **ValidateRequiredFields()** - Virtual method for derived class validation
- **ApplyAiastSecurityLabel()** - Applies AI security metadata

### Creating Custom Builders

To create a new builder:

1. **Extend the base class:**
```csharp
public class MyCustomBuilder : AiResourceBuilderBase<Observation>
{
    public MyCustomBuilder(AiInferenceConfiguration configuration)
        : base(configuration) { }
}
```

2. **Override fluent methods:**
```csharp
public new MyCustomBuilder WithInferenceId(string id)
{
    base.WithInferenceId(id);
    return this;
}

public new MyCustomBuilder WithCriteria(string id, string display, string? system = null)
{
    base.WithCriteria(id, display, system);
    return this;
}
```

3. **Add resource-specific methods:**
```csharp
public MyCustomBuilder WithSpecificProperty(string value)
{
    _specificProperty = value;
    return this;
}
```

4. **Implement validation:**
```csharp
protected override void ValidateRequiredFields()
{
    if (string.IsNullOrWhiteSpace(_specificProperty))
    {
        throw new InvalidOperationException("Specific property is required");
    }
}
```

5. **Implement build logic:**
```csharp
protected override Observation BuildCore()
{
    return new Observation
    {
        Status = ObservationStatus.Final,
        // ... other properties
    };
}
```

### Complete Example

```csharp
public class ExampleObservationBuilder : AiResourceBuilderBase<Observation>
{
    private string? _patientId;
    private string? _value;

    public ExampleObservationBuilder(AiInferenceConfiguration configuration)
        : base(configuration) { }

    public new ExampleObservationBuilder WithInferenceId(string id)
    {
        base.WithInferenceId(id);
        return this;
    }

    public ExampleObservationBuilder WithPatient(string patientId)
    {
        _patientId = patientId ?? throw new ArgumentNullException(nameof(patientId));
        return this;
    }

    public ExampleObservationBuilder WithValue(string value)
    {
        _value = value ?? throw new ArgumentNullException(nameof(value));
        return this;
    }

    protected override void ValidateRequiredFields()
    {
        if (string.IsNullOrWhiteSpace(_patientId))
            throw new InvalidOperationException("Patient ID is required");
        if (string.IsNullOrWhiteSpace(_value))
            throw new InvalidOperationException("Value is required");
    }

    protected override Observation BuildCore()
    {
        return new Observation
        {
            Status = ObservationStatus.Final,
            Subject = new ResourceReference($"Patient/{_patientId}"),
            Value = new FhirString(_value),
            Code = new CodeableConcept
            {
                Text = "Example observation"
            }
        };
    }
}

// Usage
var observation = new ExampleObservationBuilder(config)
    .WithInferenceId("example-001")
    .WithPatient("patient-123")
    .WithValue("positive")
    .WithCriteria("EXAMPLE-v1.0", "Example Criteria")
    .AddDerivedFrom("DocumentReference/source-doc")
    .Build();
```

## Helper Classes

### FhirIdGenerator
Provides standardized ID generation for FHIR resources.

#### Methods
- `GenerateInferenceId()` - Creates unique inference IDs
- `GenerateResourceId(string prefix)` - Creates prefixed resource IDs
- `IsValidFhirId(string id)` - Validates FHIR ID format

#### Examples
```csharp
var inferenceId = FhirIdGenerator.GenerateInferenceId();
// Result: "to.io-a1b2c3d4-e5f6-7890-abcd-ef1234567890"

var resourceId = FhirIdGenerator.GenerateResourceId("obs");
// Result: "obs-a1b2c3d4-e5f6-7890-abcd-ef1234567890"

bool isValid = FhirIdGenerator.IsValidFhirId("patient-123");
// Result: true
```

### FhirCodingHelper
Provides standardized clinical codes and coding utilities.

#### Code Systems
```csharp
public static class Systems
{
    public const string SNOMED_SYSTEM = "http://snomed.info/sct";
    public const string LOINC_SYSTEM = "http://loinc.org";
    public const string ICD10_SYSTEM = "http://hl7.org/fhir/sid/icd-10-cm";
}
```

#### SNOMED Codes
```csharp
public static class SnomedCodes
{
    public const string CASTRATION_SENSITIVE = "1197209002";
    public const string CASTRATION_RESISTANT = "445848006";
    public const string PROGRESSIVE_DISEASE = "277022003";
    public const string STABLE_DISEASE = "359746009";
    public const string AI_ALGORITHM = "706689003";
}
```

#### LOINC Codes
```csharp
public static class LoincCodes
{
    public const string CANCER_DISEASE_STATUS = "21889-1";
    public const string PSA_PROGRESSION = "97509-4";
    public const string PSA_MEASUREMENT = "33747-0";
}
```

#### Utility Methods
```csharp
// Create SNOMED concept
var concept = FhirCodingHelper.CreateSnomedConcept(
    "1197209002", "Castration-sensitive prostate cancer");

// Create LOINC concept
var loincConcept = FhirCodingHelper.CreateLoincConcept(
    "21889-1", "Cancer disease status");

// Create multi-system concept
var multiConcept = FhirCodingHelper.CreateMultiSystemConcept(
    new[] {
        ("http://snomed.info/sct", "1197209002", "Castration-sensitive"),
        ("http://hl7.org/fhir/sid/icd-10-cm", "Z19.1", "Hormone sensitive")
    },
    "Castration-sensitive prostate cancer"
);
```

## Best Practices

### Builder Implementation
1. **Always call base methods** in overridden fluent methods
2. **Validate required fields** in `ValidateRequiredFields()`
3. **Use meaningful error messages** for validation failures
4. **Document all public methods** with XML comments
5. **Include usage examples** in method documentation

### Error Handling
```csharp
// Good: Specific error messages
if (string.IsNullOrWhiteSpace(_patientId))
    throw new InvalidOperationException("Patient reference is required. Call WithPatient() before Build().");

// Avoid: Generic error messages
if (_patientId == null)
    throw new Exception("Missing data");
```

### Testing
```csharp
[Fact]
public void Build_WithoutPatient_ThrowsInvalidOperationException()
{
    // Arrange
    var builder = new MyBuilder(config);

    // Act & Assert
    var exception = Should.Throw<InvalidOperationException>(() => builder.Build());
    exception.Message.ShouldContain("Patient reference is required");
}

[Fact]
public void Build_WithValidData_CreatesResource()
{
    // Arrange
    var builder = new MyBuilder(config);

    // Act
    var resource = builder
        .WithPatient("Patient/123")
        .WithValue("test")
        .Build();

    // Assert
    resource.ShouldNotBeNull();
    resource.Meta.Security.Any(s => s.Code == "AIAST").ShouldBeTrue();
}
```

## Thread Safety

The base builder classes are **not thread-safe**. Each builder instance should be used by a single thread. For concurrent scenarios, create separate builder instances:

```csharp
// Good: One builder per thread
var tasks = patientIds.Select(async patientId =>
{
    var builder = new MyBuilder(config); // New instance per task
    return await ProcessPatientAsync(patientId, builder);
});

await Task.WhenAll(tasks);
```