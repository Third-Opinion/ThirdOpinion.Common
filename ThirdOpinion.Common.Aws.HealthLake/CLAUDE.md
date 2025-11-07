# ThirdOpinion.Common.Aws.HealthLake

## Purpose

This library provides comprehensive services for interacting with AWS HealthLake FHIR datastores, including writing FHIR
resources, managing HTTP operations with AWS authentication, and handling HealthLake-specific configurations.

## Core Components

### Main Services

- **HealthLakeFhirService** - Primary service for writing FHIR resources to HealthLake with version control and
  validation
- **IFhirDestinationService** - Interface for FHIR resource writing operations
- **IFhirSourceService** - Interface for FHIR resource reading operations

### HTTP & Authentication

- **IHealthLakeHttpService** - Handles AWS-signed HTTP requests to HealthLake endpoints
- **HealthLakeHttpClient** - HTTP client wrapper with AWS authentication
- **AwsCredentialsProvider** - Manages AWS credentials for HealthLake access

### Configuration & Settings

- **HealthLakeConfig** - Configuration model for HealthLake datastore settings
- **HealthLakeOptions** - Options pattern configuration for dependency injection

### Error Handling

- **HealthLakeException** - Base exception for HealthLake operations
- **HealthLakeAccessDeniedException** - Access denied specific exceptions
- **HealthLakeConflictException** - Version conflict handling
- **HealthLakeThrottlingException** - Rate limiting exception handling

### Resilience & Performance

- **RateLimitingService** - Manages API rate limiting to prevent throttling
- **RetryPolicyService** - Configurable retry policies for transient failures
- **ConcurrencyLimiter** - Controls parallel operations to HealthLake

## Usage Patterns

### Basic FHIR Resource Writing

```csharp
// Write a single FHIR resource
await healthLakeFhirService.PutResourceAsync(
    resourceType: "Patient",
    resourceId: "patient-123",
    resourceJson: patientJsonString
);

// Write with version control
var version = await healthLakeFhirService.PutResourceAsync(
    resourceType: "Patient",
    resourceId: "patient-123",
    resourceJson: updatedPatientJson,
    ifMatchVersion: "2" // Only update if current version is 2
);
```

### Batch Operations

```csharp
// Write multiple resources concurrently
var resources = new List<(string ResourceType, string ResourceId, string ResourceJson)>
{
    ("Patient", "patient-1", patient1Json),
    ("Observation", "obs-1", observation1Json),
    ("Condition", "cond-1", condition1Json)
};

var results = await healthLakeFhirService.PutResourcesAsync(resources);

// Check results
foreach (var result in results)
{
    Console.WriteLine($"{result.Key}: {(result.Value ? "Success" : "Failed")}");
}
```

### Generic Resource Writing

```csharp
// Write strongly-typed resources
var patient = new Patient { Id = "patient-123", /* other properties */ };
await healthLakeFhirService.PutResourceAsync("Patient", "patient-123", patient);

// Write with generic interface
var writeResult = await healthLakeFhirService.WriteResourceAsync(patientJsonString);
if (!writeResult.Success)
{
    Console.WriteLine($"Failed: {writeResult.ErrorMessage}");
    if (writeResult.IsRetryable)
    {
        // Implement retry logic
    }
}
```

## Configuration

### Dependency Injection Setup

```csharp
services.Configure<HealthLakeConfig>(options =>
{
    options.Region = "us-east-1";
    options.DatastoreId = "your-datastore-id";
    options.ValidationLevel = "strict"; // or "relaxed"
    options.MaxConcurrentRequests = 10;
});

services.AddHealthLakeServices();
// or with custom configuration
services.AddHealthLakeServices(config =>
{
    config.Region = "us-west-2";
    config.DatastoreId = "datastore-abc123";
});
```

### AWS Authentication

```csharp
// Uses default AWS credential chain (recommended for production)
services.AddDefaultAWSOptions(Configuration.GetAWSOptions());

// or configure explicitly
services.Configure<AWSOptions>(options =>
{
    options.Region = RegionEndpoint.USEast1;
    options.Credentials = new BasicAWSCredentials(accessKey, secretKey);
});
```

## Supported FHIR Resource Types

The service supports all major FHIR R4 resource types including:

- **Clinical**: Patient, Practitioner, Organization, Encounter, Observation, Condition, Procedure
- **Medications**: Medication, MedicationRequest, MedicationDispense, MedicationStatement
- **Documents**: DocumentReference, Binary, Media, Composition
- **Administrative**: Coverage, Location, HealthcareService, Endpoint
- **Workflow**: Task, ServiceRequest, CarePlan, CareTeam
- **Infrastructure**: Bundle, Provenance, AuditEvent, Consent

Check supported types:

```csharp
var supportedTypes = healthLakeFhirService.GetSupportedResourceTypes();
bool isSupported = healthLakeFhirService.IsResourceTypeSupported("Patient");
```

## Error Handling & Resilience

### Exception Types

```csharp
try
{
    await healthLakeFhirService.PutResourceAsync("Patient", "123", patientJson);
}
catch (HealthLakeAccessDeniedException ex)
{
    // Handle access denied - check IAM permissions
    logger.LogError("Access denied to HealthLake: {Message}", ex.Message);
}
catch (HealthLakeConflictException ex)
{
    // Handle version conflicts - resource was modified
    logger.LogWarning("Version conflict for {ResourceType}/{ResourceId}: {Message}",
        ex.ResourceType, ex.ResourceId, ex.Message);
}
catch (HealthLakeThrottlingException ex)
{
    // Handle rate limiting - wait before retry
    await Task.Delay(ex.RetryAfter ?? TimeSpan.FromSeconds(30));
}
catch (HealthLakeException ex)
{
    // Handle general HealthLake errors
    logger.LogError("HealthLake error: {Message} (StatusCode: {StatusCode})",
        ex.Message, ex.StatusCode);
}
```

### Retry Strategies

```csharp
// Built-in concurrency limiting (max 10 concurrent requests by default)
// Automatic retry handling for transient failures

// Custom retry logic for specific scenarios
var maxRetries = 3;
var retryDelay = TimeSpan.FromSeconds(1);

for (int attempt = 0; attempt < maxRetries; attempt++)
{
    try
    {
        var result = await healthLakeFhirService.WriteResourceAsync(resourceJson);
        if (result.Success || !result.IsRetryable)
            break;

        if (attempt < maxRetries - 1)
            await Task.Delay(retryDelay * (attempt + 1)); // Exponential backoff
    }
    catch (HealthLakeThrottlingException ex)
    {
        await Task.Delay(ex.RetryAfter ?? TimeSpan.FromMinutes(1));
    }
}
```

## Best Practices

### Performance Optimization

```csharp
// Use batch operations for multiple resources
var batchResults = await healthLakeFhirService.PutResourcesAsync(resourceList);

// Built-in concurrency limiting prevents overwhelming HealthLake
// Default: 10 concurrent requests (configurable)

// Monitor performance with logging
services.AddLogging(builder =>
{
    builder.SetMinimumLevel(LogLevel.Information);
    builder.AddApplicationInsights(); // or other providers
});
```

### Version Control

```csharp
// Always use version control for updates to prevent conflicts
var currentResource = await GetResourceFromHealthLake("Patient", "123");
var currentVersion = ExtractVersionFromResource(currentResource);

var updatedVersion = await healthLakeFhirService.PutResourceAsync(
    "Patient", "123", updatedResourceJson, currentVersion);
```

### Resource Validation

```csharp
// HealthLake performs strict FHIR validation by default
// Ensure your resources conform to FHIR R4 specification

// The service adds strict validation headers automatically:
// x-amz-fhir-validation-level: strict

// Handle validation errors appropriately
catch (HealthLakeException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
{
    // Resource failed FHIR validation
    logger.LogError("FHIR validation failed: {Message}", ex.Message);
}
```

### Monitoring & Observability

```csharp
// Built-in logging with structured data
// Correlation IDs for request tracking
// Performance metrics for operation duration
// Success/failure rates for batch operations

// Configure logging levels appropriately:
// - Information: Operation summaries, batch results
// - Debug: Detailed request/response info, version extraction
// - Warning: Validation failures, version conflicts
// - Error: Access denied, service unavailable
```

## Common Integration Patterns

### FHIR Pipeline Processing

```csharp
// Process FHIR resources from various sources
public async Task ProcessFhirBundle(Bundle bundle)
{
    var resources = ExtractResourcesFromBundle(bundle);
    var resourceTuples = resources.Select(r =>
        (r.ResourceType, r.Id, JsonSerializer.Serialize(r))).ToList();

    var results = await healthLakeFhirService.PutResourcesAsync(resourceTuples);

    // Handle partial failures
    var failures = results.Where(r => !r.Value);
    foreach (var failure in failures)
    {
        logger.LogWarning("Failed to write resource: {ResourceKey}", failure.Key);
    }
}
```

### Health Information Exchange (HIE)

```csharp
// Standardized pattern for HIE data ingestion
public async Task IngestHieData(IEnumerable<FhirResource> resources)
{
    foreach (var resource in resources)
    {
        try
        {
            // Validate and normalize resource
            var normalizedResource = await NormalizeForHealthLake(resource);

            // Write to HealthLake with proper error handling
            await healthLakeFhirService.PutResourceAsync(
                normalizedResource.ResourceType,
                normalizedResource.Id,
                JsonSerializer.Serialize(normalizedResource)
            );
        }
        catch (HealthLakeException ex)
        {
            // Log and continue with next resource
            logger.LogError("Failed to ingest {ResourceType}/{ResourceId}: {Message}",
                resource.ResourceType, resource.Id, ex.Message);
        }
    }
}
```