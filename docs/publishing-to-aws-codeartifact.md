# Publishing to AWS CodeArtifact

This document describes how to publish the ThirdOpinion.Common NuGet package to AWS CodeArtifact.

## Prerequisites

1. **AWS CLI**: Install and configure AWS CLI with appropriate credentials
2. **AWS Permissions**: Ensure your AWS credentials have the following permissions:
   - `codeartifact:GetAuthorizationToken`
   - `codeartifact:GetRepositoryEndpoint`
   - `codeartifact:PublishPackageVersion`
   - `codeartifact:PutPackageMetadata`

3. **CodeArtifact Repository**: The repository must exist in AWS CodeArtifact
   - Domain: `thirdopinion`
   - Repository: `Thirdopinion_Nuget`
   - Region: `us-east-2`

## Configuration

The publishing process uses the `aws-codeartifact-config.json` file for configuration:

```json
{
  "codeArtifact": {
    "domain": "thirdopinion",
    "accountId": "442042533707",
    "region": "us-east-2",
    "repository": "Thirdopinion_Nuget"
  },
  "package": {
    "version": "1.0.0-alpha.1",
    "configuration": "Release",
    "skipTests": false
  },
  "aws": {
    "profile": "default",
    "region": "us-east-2"
  },
  "nuget": {
    "sourceName": "aws-codeartifact",
    "packagesPath": "packages"
  }
}
```

## Publishing Methods

### Method 1: Complete Build and Publish Pipeline

Run the complete pipeline that builds, tests, and publishes:

```powershell
# From the project root directory
.\scripts\build-and-publish.ps1
```

With custom parameters:

```powershell
.\scripts\build-and-publish.ps1 -Configuration Release -SkipTests -Version "1.0.0-alpha.2"
```

### Method 2: Publish Only (if already built)

If you've already built the packages and just want to publish:

```powershell
.\scripts\publish-to-codeartifact.ps1 -SkipBuild
```

### Method 3: Build Only (without publishing)

To build packages without publishing to CodeArtifact:

```powershell
.\scripts\build-and-publish.ps1 -SkipPublish
```

## Script Parameters

### build-and-publish.ps1

- `-Configuration`: Build configuration (default: "Release")
- `-ConfigFile`: Path to configuration file (default: "../aws-codeartifact-config.json")
- `-SkipTests`: Skip running unit tests
- `-SkipPublish`: Skip publishing to CodeArtifact
- `-Version`: Override version from config file

### publish-to-codeartifact.ps1

- `-Configuration`: Build configuration (default: "Release")
- `-ConfigFile`: Path to configuration file (default: "../aws-codeartifact-config.json")
- `-SkipTests`: Skip running unit tests
- `-SkipBuild`: Skip building (assume packages already exist)
- `-Version`: Override version from config file

## Publishing Process

The publishing process performs the following steps:

1. **Verification**: Checks AWS CLI installation and credentials
2. **Authentication**: Gets CodeArtifact authorization token
3. **Repository Setup**: Gets repository endpoint URL
4. **Build** (if not skipped): Cleans, restores, builds, and tests
5. **Package**: Creates NuGet package with specified version
6. **Configure NuGet**: Sets up CodeArtifact as a NuGet source
7. **Publish**: Uploads package to CodeArtifact
8. **Cleanup**: Removes temporary NuGet source configuration

## Version Management

### Pre-release Versions

The project is configured for pre-release versions using semantic versioning:

- **Alpha**: `1.0.0-alpha.1`, `1.0.0-alpha.2`, etc.
- **Beta**: `1.0.0-beta.1`, `1.0.0-beta.2`, etc.
- **RC**: `1.0.0-rc.1`, `1.0.0-rc.2`, etc.

### Version Updates

To update the version:

1. **Update configuration file**: Modify `version` in `aws-codeartifact-config.json`
2. **Update project files**: The build scripts will use the version from the config file

### Assembly Version

The assembly version remains at `1.0.0.0` for pre-release versions to maintain compatibility.

## Consuming the Package

After publishing, other projects can consume the package by:

1. **Configure CodeArtifact source**:
   ```powershell
   $authToken = aws codeartifact get-authorization-token --domain thirdopinion --domain-owner 442042533707 --region us-east-2 --query authorizationToken --output text
   dotnet nuget add source https://thirdopinion-442042533707.d.codeartifact.us-east-2.amazonaws.com/nuget/Thirdopinion_Nuget/ --name aws-codeartifact --username aws --password $authToken --store-password-in-clear-text
   ```

2. **Add package reference**:
   ```powershell
   dotnet add package ThirdOpinion.Common --version 1.0.0-alpha.1 --source aws-codeartifact
   ```

3. **Or add to .csproj**:
   ```xml
   <PackageReference Include="ThirdOpinion.Common" Version="1.0.0-alpha.1" />
   ```

## Troubleshooting

### Common Issues

1. **AWS Credentials Error**
   ```
   AWS credentials not configured or invalid
   ```
   - Solution: Run `aws configure` or set up AWS credentials
   - Verify with: `aws sts get-caller-identity --profile default`

2. **CodeArtifact Permission Denied**
   ```
   Failed to get CodeArtifact authorization token
   ```
   - Solution: Ensure your AWS user/role has CodeArtifact permissions
   - Check IAM policies for CodeArtifact access

3. **Repository Not Found**
   ```
   Failed to get CodeArtifact repository endpoint
   ```
   - Solution: Verify domain and repository names in config
   - Ensure repository exists in the specified region

4. **Package Already Exists**
   ```
   Package version already exists
   ```
   - Solution: Increment version number in configuration
   - Or delete existing version from CodeArtifact (if needed)

### Debug Mode

Run scripts with verbose output:

```powershell
.\scripts\publish-to-codeartifact.ps1 -Verbose
```

### Manual Steps

If automated scripts fail, you can perform manual steps:

1. **Get authorization token**:
   ```powershell
   aws codeartifact get-authorization-token --domain thirdopinion --domain-owner 442042533707 --region us-east-2
   ```

2. **Configure NuGet source**:
   ```powershell
   dotnet nuget add source https://thirdopinion-442042533707.d.codeartifact.us-east-2.amazonaws.com/nuget/Thirdopinion_Nuget/ --name aws-codeartifact --username aws --password <token>
   ```

3. **Publish package**:
   ```powershell
   dotnet nuget push packages\ThirdOpinion.Common.1.0.0-alpha.1.nupkg --source aws-codeartifact --api-key <token>
   ```

## Security Considerations

1. **Authorization Tokens**: CodeArtifact tokens expire after 12 hours
2. **Credentials**: Store AWS credentials securely using AWS CLI or environment variables
3. **Package Sources**: Remove temporary NuGet sources after publishing
4. **Version Control**: Do not commit authorization tokens or sensitive configuration

## CI/CD Integration

For automated publishing in CI/CD pipelines:

1. **Set environment variables**:
   ```bash
   export AWS_ACCESS_KEY_ID=your_access_key
   export AWS_SECRET_ACCESS_KEY=your_secret_key
   export AWS_DEFAULT_REGION=us-east-2
   ```

2. **Run publish script**:
   ```powershell
   .\scripts\build-and-publish.ps1 -Configuration Release
   ```

3. **Use specific AWS profile** (if needed):
   ```powershell
   .\scripts\publish-to-codeartifact.ps1 -ConfigFile aws-codeartifact-config.json
   ```

## Package Contents

The published package includes:

- **ThirdOpinion.Common.Aws.Cognito**: AWS Cognito integration
- **ThirdOpinion.Common.Aws.DynamoDb**: DynamoDB repository pattern
- **ThirdOpinion.Common.Aws.S3**: S3 storage utilities
- **ThirdOpinion.Common.Aws.SQS**: SQS message queue integration
- **ThirdOpinion.Common.Misc**: Utility functions
- **ThirdOpinion.Common.Fhir**: FHIR R4 healthcare integration

All dependencies are included as package references, ensuring consumers get the complete functionality.


