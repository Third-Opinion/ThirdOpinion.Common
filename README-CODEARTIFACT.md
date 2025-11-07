# AWS CodeArtifact Publishing Setup

This project is configured to publish NuGet packages to AWS CodeArtifact.

## Quick Start

### Prerequisites

1. AWS CLI installed and configured
2. AWS credentials with CodeArtifact permissions
3. Access to the `thirdopinion` CodeArtifact domain

### Publish Package

```powershell
# Complete build and publish pipeline
.\scripts\build-and-publish.ps1

# Or publish only (if already built)
.\scripts\publish-to-codeartifact.ps1 -SkipBuild
```

### Verify AWS Setup

```powershell
# Check AWS credentials
aws sts get-caller-identity --profile default

# Test CodeArtifact access
aws codeartifact list-domains --region us-east-2
```

## Configuration

Edit `aws-codeartifact-config.json` to modify:

- Package version
- AWS profile
- Build configuration
- Skip tests option

## Current Configuration

- **Domain**: `thirdopinion`
- **Repository**: `Thirdopinion_Nuget`
- **Region**: `us-east-2`
- **Account**: `442042533707`
- **Version**: `1.0.0-alpha.1`

## Package Contents

The published package includes:

- AWS Cognito integration
- DynamoDB repository pattern
- S3 storage utilities
- SQS message queue integration
- FHIR R4 healthcare integration
- Utility functions

## Documentation

See [docs/publishing-to-aws-codeartifact.md](docs/publishing-to-aws-codeartifact.md) for detailed instructions.

## Troubleshooting

Common issues and solutions:

1. **AWS credentials not found**: Run `aws configure`
2. **Permission denied**: Check IAM policies for CodeArtifact access
3. **Repository not found**: Verify domain/repository names
4. **Package exists**: Increment version number

For more help, see the detailed documentation or run scripts with `-Verbose` flag.