# ThirdOpinion.Common.Aws.HealthLake

AWS HealthLake FHIR datastore integration for ThirdOpinion applications.

## Overview

This library provides integration with AWS HealthLake for managing FHIR R4 resources. It includes:

- HealthLake FHIR service implementation
- FHIR resource CRUD operations
- Search and query capabilities
- Batch operations support

## Installation

```bash
dotnet add package ThirdOpinion.Common.Aws.HealthLake
```

## Usage

```csharp
// Add to DI container
services.AddHealthLakeFhirService(configuration);

// Inject and use
public class MyService
{
    private readonly IHealthLakeFhirService _healthLakeService;

    public MyService(IHealthLakeFhirService healthLakeService)
    {
        _healthLakeService = healthLakeService;
    }
}
```

## Configuration

Configure in appsettings.json:

```json
{
  "AWS": {
    "HealthLake": {
      "DatastoreId": "your-datastore-id",
      "Region": "us-east-1"
    }
  }
}
```