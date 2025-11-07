# ThirdOpinion.Common.AthenaEhr

Athena EHR integration with OAuth and FHIR support for ThirdOpinion applications.

## Overview

This library provides integration with Athena EHR system, including:

- OAuth 2.0 authentication flow
- FHIR R4 resource operations
- Token management and refresh
- Resilient HTTP client with retry policies

## Installation

```bash
dotnet add package ThirdOpinion.Common.AthenaEhr
```

## Usage

```csharp
// Add to DI container
services.AddAthenaEhr(configuration);

// Inject and use
public class MyService
{
    private readonly IFhirSourceService _athenaService;
    private readonly IAthenaOAuthService _authService;

    public MyService(IFhirSourceService athenaService, IAthenaOAuthService authService)
    {
        _athenaService = athenaService;
        _authService = authService;
    }
}
```

## Configuration

Configure in appsettings.json:

```json
{
  "Athena": {
    "BaseUrl": "https://api.athenahealth.com",
    "ClientId": "your-client-id",
    "ClientSecret": "your-client-secret",
    "PracticeId": "your-practice-id",
    "RequestTimeoutSeconds": 30
  }
}
```