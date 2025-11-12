# ThirdOpinion.Common Usage Guide

## Overview

ThirdOpinion.Common is a comprehensive collection of .NET libraries designed to simplify development of healthcare
applications with AWS integration, FHIR R4 support, and utility functions. The package suite provides production-ready
components for common patterns in healthcare software development.

## Package Structure

### Core Packages

#### ThirdOpinion.Common

The meta-package that includes all sub-packages as dependencies. Install this for comprehensive functionality.

```bash
dotnet add package ThirdOpinion.Common
```

### AWS Integration Packages

#### ThirdOpinion.Common.Aws.Cognito

**Purpose**: AWS Cognito authentication and user management
**Key Features**:

- JWT token validation and management
- User pool operations
- Group-based authorization
- Custom authentication flows

**Usage Example**:

```csharp
using ThirdOpinion.Common.Aws.Cognito;

// Configure in Startup.cs/Program.cs
services.AddCognitoAuthentication(options =>
{
    options.UserPoolId = "us-east-2_XXXXXXXXX";
    options.Region = "us-east-2";
    options.ClientId = "your-client-id";
});

// Use in controller
[Authorize]
public class SecureController : ControllerBase
{
    private readonly ICognitoUserService _userService;

    public SecureController(ICognitoUserService userService)
    {
        _userService = userService;
    }

    [HttpGet("profile")]
    public async Task<ActionResult> GetUserProfile()
    {
        var user = await _userService.GetCurrentUserAsync();
        return Ok(user);
    }
}
```

#### ThirdOpinion.Common.Aws.DynamoDb

**Purpose**: DynamoDB operations with repository pattern
**Key Features**:

- Generic repository implementation
- Query and scan operations
- Batch operations
- Index support

**Usage Example**:

```csharp
using ThirdOpinion.Common.Aws.DynamoDb;

// Configure
services.AddDynamoDbRepository<Patient>("PatientTable");

// Use repository
public class PatientService
{
    private readonly IDynamoDbRepository<Patient> _repository;

    public PatientService(IDynamoDbRepository<Patient> repository)
    {
        _repository = repository;
    }

    public async Task<Patient> CreatePatientAsync(Patient patient)
    {
        return await _repository.CreateAsync(patient);
    }

    public async Task<List<Patient>> GetPatientsByProviderAsync(string providerId)
    {
        return await _repository.QueryAsync(
            keyName: "ProviderId",
            keyValue: providerId
        );
    }
}
```

#### ThirdOpinion.Common.Aws.S3

**Purpose**: S3 storage operations
**Key Features**:

- Upload/download operations
- Presigned URL generation
- Metadata management
- Multipart upload support

**Usage Example**:

```csharp
using ThirdOpinion.Common.Aws.S3;

// Configure
services.AddS3Service(options =>
{
    options.BucketName = "my-healthcare-bucket";
    options.Region = "us-east-2";
});

// Use service
public class DocumentService
{
    private readonly IS3Service _s3Service;

    public async Task<string> UploadDocumentAsync(Stream fileStream, string fileName)
    {
        var result = await _s3Service.UploadAsync(
            key: $"documents/{fileName}",
            content: fileStream,
            contentType: "application/pdf"
        );

        return result.Location;
    }

    public async Task<string> GetDownloadUrlAsync(string documentKey)
    {
        return await _s3Service.GeneratePresignedUrlAsync(
            key: documentKey,
            expiration: TimeSpan.FromHours(1)
        );
    }
}
```

#### ThirdOpinion.Common.Aws.SQS

**Purpose**: SQS message queuing
**Key Features**:

- Message publishing and consuming
- Dead letter queue support
- Batch operations
- Message attributes

**Usage Example**:

```csharp
using ThirdOpinion.Common.Aws.SQS;

// Configure
services.AddSqsService(options =>
{
    options.QueueUrl = "https://sqs.us-east-2.amazonaws.com/123456789/my-queue";
});

// Use service
public class NotificationService
{
    private readonly ISqsService _sqsService;

    public async Task SendPatientUpdateNotificationAsync(PatientUpdateEvent updateEvent)
    {
        await _sqsService.SendMessageAsync(new SqsMessage
        {
            Body = JsonSerializer.Serialize(updateEvent),
            MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                ["EventType"] = new MessageAttributeValue
                {
                    StringValue = "PatientUpdate",
                    DataType = "String"
                }
            }
        });
    }
}
```

#### ThirdOpinion.Common.Aws.HealthLake

**Purpose**: AWS HealthLake FHIR datastore integration
**Key Features**:

- FHIR R4 resource operations
- Bulk import/export
- Search capabilities
- Transaction support

**Usage Example**:

```csharp
using ThirdOpinion.Common.Aws.HealthLake;

// Configure
services.AddHealthLakeService(options =>
{
    options.DatastoreId = "836e877666cebf177ce6370ec1478a92";
    options.Region = "us-east-2";
});

// Use service
public class FhirService
{
    private readonly IHealthLakeFhirService _fhirService;

    public async Task<string> CreatePatientAsync(Patient patient)
    {
        var response = await _fhirService.PutResourceAsync(
            resourceType: "Patient",
            resourceId: patient.Id,
            resourceJson: JsonSerializer.Serialize(patient)
        );

        return response.VersionId;
    }

    public async Task<Patient> GetPatientAsync(string patientId)
    {
        var json = await _fhirService.GetResourceAsync("Patient", patientId);
        return JsonSerializer.Deserialize<Patient>(json);
    }
}
```

#### ThirdOpinion.Common.Aws.Bedrock

**Purpose**: AWS Bedrock AI/ML service integration
**Key Features**:

- Claude model integration
- Text generation
- Cost tracking
- Langfuse observability

**Usage Example**:

```csharp
using ThirdOpinion.Common.Aws.Bedrock;

// Configure
services.AddBedrockService(options =>
{
    options.Region = "us-east-2";
    options.DefaultModel = "anthropic.claude-3-sonnet-20240229-v1:0";
});

// Use service
public class AiService
{
    private readonly IBedrockService _bedrockService;

    public async Task<string> GeneratePatientSummaryAsync(string patientData)
    {
        var request = new ClaudeRequest
        {
            MaxTokens = 1000,
            Messages = new[]
            {
                new ClaudeMessage
                {
                    Role = "user",
                    Content = $"Summarize this patient data: {patientData}"
                }
            }
        };

        var response = await _bedrockService.InvokeClaudeAsync(request);
        return response.Content.First().Text;
    }
}
```

### FHIR Integration Packages

#### ThirdOpinion.Common.Fhir

**Purpose**: FHIR R4 resource building and validation
**Key Features**:

- Resource builders for common FHIR resources
- Validation helpers
- Extension management
- Profile support

**Usage Example**:

```csharp
using ThirdOpinion.Common.Fhir;

// Build a Patient resource
var patient = new PatientBuilder()
    .WithId("patient-123")
    .WithName("John", "Doe")
    .WithBirthDate(new DateTime(1980, 1, 1))
    .WithGender(AdministrativeGender.Male)
    .WithPhone("555-1234")
    .WithEmail("john.doe@example.com")
    .Build();

// Build an Observation
var observation = new ObservationBuilder()
    .WithId("obs-456")
    .WithPatient("Patient/patient-123")
    .WithCode("8480-6", "http://loinc.org", "Systolic blood pressure")
    .WithValue(120, "mm[Hg]")
    .WithEffectiveDate(DateTime.UtcNow)
    .WithStatus(ObservationStatus.Final)
    .Build();
```

#### ThirdOpinion.Common.Fhir.Documents

**Purpose**: FHIR document handling and processing
**Key Features**:

- Document reference management
- File upload/download
- Document validation
- Bulk processing

**Usage Example**:

```csharp
using ThirdOpinion.Common.Fhir.Documents;

// Configure
services.AddFhirDocumentService(options =>
{
    options.StorageBucket = "fhir-documents";
    options.AllowedFileTypes = new[] { ".pdf", ".jpg", ".png", ".dcm" };
    options.MaxFileSizeBytes = 50 * 1024 * 1024; // 50MB
});

// Use service
public class DocumentService
{
    private readonly IFhirDocumentService _documentService;

    public async Task<DocumentReference> UploadDocumentAsync(
        Stream fileStream,
        string fileName,
        string patientId)
    {
        var result = await _documentService.UploadDocumentAsync(new DocumentUploadRequest
        {
            Content = fileStream,
            FileName = fileName,
            ContentType = "application/pdf",
            PatientId = patientId,
            Category = "clinical-note"
        });

        return result.DocumentReference;
    }
}
```

### Utility Packages

#### ThirdOpinion.Common.Misc

**Purpose**: Utility functions and helpers
**Key Features**:

- Retry policies
- Rate limiting
- Validation helpers
- Extension methods

**Usage Example**:

```csharp
using ThirdOpinion.Common.Misc.Retry;
using ThirdOpinion.Common.Misc.RateLimiting;

// Configure retry policies
services.AddRetryPolicies();

// Use retry policy
public class ApiService
{
    private readonly IRetryPolicyService _retryService;

    public async Task<string> CallExternalApiAsync()
    {
        var policy = _retryService.GetRetryPolicy("ExternalApi");

        return await policy.ExecuteAsync(async () =>
        {
            // API call that might fail
            return await httpClient.GetStringAsync("https://api.example.com/data");
        });
    }
}

// Rate limiting
services.AddRateLimiting(options =>
{
    options.AddPolicy("Api", limiter =>
        limiter.SetPermitLimit(100)
               .SetWindow(TimeSpan.FromMinutes(1)));
});
```

#### ThirdOpinion.Common.Logging

**Purpose**: Structured logging with correlation IDs
**Key Features**:

- Correlation ID management
- Structured logging configuration
- Request/response logging middleware
- Performance tracking

**Usage Example**:

```csharp
using ThirdOpinion.Common.Logging;

// Configure
services.AddStructuredLogging(options =>
{
    options.ApplicationName = "HealthcareApi";
    options.Environment = "Production";
    options.EnableRequestLogging = true;
});

// Use correlation ID
public class PatientController : ControllerBase
{
    private readonly ILogger<PatientController> _logger;
    private readonly ICorrelationIdProvider _correlationProvider;

    public PatientController(
        ILogger<PatientController> logger,
        ICorrelationIdProvider correlationProvider)
    {
        _logger = logger;
        _correlationProvider = correlationProvider;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult> GetPatient(string id)
    {
        var correlationId = _correlationProvider.GetCorrelationId();

        _logger.LogInformation("Retrieving patient {PatientId} with correlation {CorrelationId}",
            id, correlationId);

        // ... rest of method
    }
}
```

#### ThirdOpinion.Common.AthenaEhr

**Purpose**: Athena EHR integration
**Key Features**:

- OAuth authentication
- FHIR API integration
- Patient data synchronization
- Appointment management

**Usage Example**:

```csharp
using ThirdOpinion.Common.AthenaEhr;

// Configure
services.AddAthenaEhrIntegration(options =>
{
    options.BaseUrl = "https://api.athenahealth.com";
    options.ClientId = "your-client-id";
    options.ClientSecret = "your-client-secret";
    options.PracticeId = "your-practice-id";
});

// Use service
public class EhrService
{
    private readonly IAthenaFhirService _athenaService;

    public async Task<Patient> GetPatientFromAthenaAsync(string patientId)
    {
        return await _athenaService.GetPatientAsync(patientId);
    }

    public async Task<List<Appointment>> GetAppointmentsAsync(string patientId)
    {
        return await _athenaService.GetAppointmentsAsync(patientId);
    }
}
```

#### ThirdOpinion.Common.Langfuse

**Purpose**: Langfuse observability for AI operations
**Key Features**:

- LLM request/response tracking
- Cost monitoring
- Performance analytics
- Custom metadata

**Usage Example**:

```csharp
using ThirdOpinion.Common.Langfuse;

// Configure
services.AddLangfuseObservability(options =>
{
    options.ApiKey = "your-langfuse-api-key";
    options.BaseUrl = "https://your-langfuse-instance.com";
    options.ProjectName = "HealthcareAI";
});

// Use service
public class AiAnalyticsService
{
    private readonly ILangfuseService _langfuseService;

    public async Task TrackLlmUsageAsync(string prompt, string response, decimal cost)
    {
        await _langfuseService.TraceAsync(new LangfuseTraceRequest
        {
            Name = "patient-summary-generation",
            Input = prompt,
            Output = response,
            Metadata = new { cost, model = "claude-3-sonnet" }
        });
    }
}
```

## Configuration

### AWS Credentials

All AWS services require proper credentials configuration. Use any of the standard AWS credential providers:

```json
{
  "AWS": {
    "Region": "us-east-2",
    "Profile": "default"
  }
}
```

Or environment variables:

```bash
export AWS_ACCESS_KEY_ID=your-access-key
export AWS_SECRET_ACCESS_KEY=your-secret-key
export AWS_DEFAULT_REGION=us-east-2
```

### Complete Configuration Example

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "ThirdOpinion": "Debug"
    }
  },
  "AWS": {
    "Region": "us-east-2"
  },
  "Cognito": {
    "UserPoolId": "us-east-2_XXXXXXXXX",
    "ClientId": "your-client-id",
    "Region": "us-east-2"
  },
  "HealthLake": {
    "DatastoreId": "your-datastore-id",
    "Region": "us-east-2",
    "RequestTimeoutSeconds": 30
  },
  "S3": {
    "BucketName": "your-bucket-name",
    "Region": "us-east-2"
  },
  "SQS": {
    "QueueUrl": "https://sqs.us-east-2.amazonaws.com/123456789/your-queue"
  },
  "Bedrock": {
    "Region": "us-east-2",
    "DefaultModel": "anthropic.claude-3-sonnet-20240229-v1:0"
  },
  "Athena": {
    "BaseUrl": "https://api.athenahealth.com",
    "ClientId": "your-client-id",
    "ClientSecret": "your-client-secret",
    "PracticeId": "your-practice-id"
  },
  "Langfuse": {
    "ApiKey": "your-langfuse-api-key",
    "BaseUrl": "https://your-langfuse-instance.com",
    "ProjectName": "HealthcareAI"
  }
}
```

## Migration from FhirTools

### Package Mapping

- `FhirTools.Aws` → `ThirdOpinion.Common.Aws.Misc`
- `FhirTools.Configuration` → `ThirdOpinion.Common.Configuration` (distributed across packages)
- `FhirTools.Fhir` → `ThirdOpinion.Common.Aws.HealthLake`
- `FhirTools.Documents` → `ThirdOpinion.Common.Fhir.Documents`
- `FhirTools.Logging` → `ThirdOpinion.Common.Logging`
- `FhirTools.RateLimiting` → `ThirdOpinion.Common.Misc.RateLimiting`
- `FhirTools.Retry` → `ThirdOpinion.Common.Misc.Retry`

### Namespace Changes

1. Update all `using FhirTools.*` statements to corresponding `ThirdOpinion.Common.*`
2. Update service registrations in `Startup.cs`/`Program.cs`
3. Update configuration section names if needed

### Breaking Changes

- Some configuration property names have changed for consistency
- Service registration methods have been renamed for clarity
- Some interfaces have been split for better separation of concerns

### Migration Steps

1. **Install New Packages**: Replace FhirTools packages with ThirdOpinion.Common packages
2. **Update Namespaces**: Use find/replace to update namespace references
3. **Update Configuration**: Review and update configuration sections
4. **Update Service Registration**: Update DI container registrations
5. **Test Thoroughly**: Run all tests to ensure functionality is preserved

## Common Patterns

### Error Handling

All services follow consistent error handling patterns:

```csharp
try
{
    var result = await _service.ProcessAsync(data);
    return result;
}
catch (ThirdOpinionException ex)
{
    // Library-specific exceptions with detailed error information
    _logger.LogError(ex, "Operation failed: {ErrorCode}", ex.ErrorCode);
    throw;
}
catch (Exception ex)
{
    // Unexpected errors
    _logger.LogError(ex, "Unexpected error occurred");
    throw;
}
```

### Async/Await

All operations are async by default:

```csharp
// Good
var patients = await _repository.GetPatientsAsync(providerId);

// Avoid
var patients = _repository.GetPatientsAsync(providerId).Result;
```

### Dependency Injection

Register all services in your DI container:

```csharp
// In Program.cs or Startup.cs
services.AddThirdOpinionCommon(configuration);

// Or register individually
services.AddCognitoAuthentication(options => { });
services.AddDynamoDbRepository<Patient>("PatientTable");
services.AddS3Service(options => { });
// ... etc
```

## Troubleshooting

### Common Issues

#### AWS Credentials

**Problem**: `AmazonServiceException: Unable to get IAM security credentials`
**Solution**: Ensure AWS credentials are properly configured via AWS CLI, environment variables, or IAM roles.

#### Package Dependencies

**Problem**: `FileNotFoundException` for AWS SDK assemblies
**Solution**: Ensure all required AWS SDK packages are installed and versions are compatible.

#### Configuration

**Problem**: `NullReferenceException` in service constructors
**Solution**: Verify all configuration sections are present and services are registered in DI container.

#### FHIR Validation

**Problem**: FHIR resources fail validation
**Solution**: Use the built-in builders and validation helpers to ensure resources conform to FHIR R4 specification.

### Logging and Debugging

Enable detailed logging for troubleshooting:

```json
{
  "Logging": {
    "LogLevel": {
      "ThirdOpinion": "Debug",
      "Amazon": "Information"
    }
  }
}
```

### Performance Considerations

- Use connection pooling for HTTP clients
- Implement proper retry policies with exponential backoff
- Cache frequently accessed data
- Use batch operations when available
- Monitor AWS costs and set up billing alarms

## Support and Contributing

For issues, questions, or contributions, please refer to the project repository and documentation.

### Version Compatibility

- .NET 8.0 or later
- AWS SDK v4.x
- FHIR R4 specification

### License

This package is licensed under the MIT License.