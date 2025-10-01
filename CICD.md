# ThirdOpinion.Common CI/CD Documentation

## Overview

This document describes the complete CI/CD pipeline setup for the ThirdOpinion.Common library packages. The pipeline is designed to provide automated testing, security scanning, and NuGet package publishing with both automatic and manual triggers.

## Repository Structure

```
ThirdOpinion.Common/
├── .github/
│   ├── workflows/
│   │   ├── ci-cd.yml           # Main CI/CD pipeline
│   └── README.md               # CI/CD documentation
├── scripts/
│   ├── build-packages.sh       # Local build script (Bash)
│   └── build-packages.ps1      # Local build script (PowerShell)
├── ThirdOpinion.Common.Aws.*/  # Library projects
├── ThirdOpinion.Common.*.UnitTests/  # Unit test projects
├── ThirdOpinion.Common.FunctionalTests/  # Functional test project
└── CICD.md                     # This file
```

## Pipeline Features

### 🚀 Automatic CI/CD
- **Triggers**: Push to main branches, pull requests, releases
- **Testing**: Unit tests with coverage reporting
- **Security**: Vulnerability scanning and CodeQL analysis
- **Publishing**: Automatic package publishing on releases

### 🎛️ Manual Controls
- **Functional Tests**: Optional AWS integration testing with LocalStack
- **Development Publishing**: Manual package publishing for testing
- **Environment Protection**: Production environment with approval gates

### 📦 Package Management
- **Multiple Packages**: 5 separate NuGet packages
- **Versioning**: Semantic versioning with development builds
- **Distribution**: GitHub Packages + NuGet.org

## Packages Published

| Package | Description | Dependencies |
|---------|-------------|--------------|
| `ThirdOpinion.Common.Aws.Cognito` | AWS Cognito utilities and authorization | AWS Cognito SDK |
| `ThirdOpinion.Common.Aws.DynamoDb` | DynamoDB repository and pagination | AWS DynamoDB SDK |
| `ThirdOpinion.Common.Aws.S3` | S3 storage abstractions | AWS S3 SDK |
| `ThirdOpinion.Common.Aws.SQS` | SQS message queue implementations | AWS SQS SDK |
| `ThirdOpinion.Common.Misc` | Miscellaneous utilities (Patient HUID, etc.) | .NET 8.0 |

## Getting Started

### 1. Repository Setup

#### Required Secrets
Add these to your GitHub repository settings:

```
NUGET_API_KEY          # NuGet.org API key for publishing packages
```

#### Environment Setup
1. Create a `production` environment in repository settings
2. Add required reviewers for production deployments
3. Configure branch protection rules for main/master

### 2. First-Time Setup

#### Local Development
```bash
# Clone and build
git clone <repository-url>
cd ThirdOpinion.Common
dotnet restore
dotnet build

# Run tests
dotnet test

# Build packages locally
./scripts/build-packages.sh
```

#### Verify CI/CD
1. Create a test branch and push changes
2. Open a pull request to trigger the pipeline
3. Verify all checks pass

## Workflow Details

### Main CI/CD Pipeline (`ci-cd.yml`)

#### Build and Test Job
```yaml
runs-on: ubuntu-latest
steps:
  - Checkout code
  - Setup .NET 8.0
  - Cache NuGet packages
  - Restore dependencies
  - Build solution (Release)
  - Run unit tests with coverage
  - Upload artifacts
```

**Triggers**: All pushes and PRs

#### Functional Tests Job
```yaml
services:
  localstack:
    image: localstack/localstack:latest
    ports: [4566:4566]
    env:
      SERVICES: cognito-idp,dynamodb,s3,sqs
```

**Triggers**: Manual workflow dispatch only

**Features**:
- Full AWS service emulation with LocalStack
- Integration testing across all AWS services
- Environment variable configuration
- Comprehensive test coverage

#### Package Creation Job
```yaml
strategy:
  matrix:
    package: [Cognito, DynamoDb, S3, SQS, Misc]
```

**Triggers**: Releases or manual dispatch

**Features**:
- Semantic versioning
- Development build versioning
- Multi-package support
- Artifact preservation

#### Publishing Job
```yaml
environment: production
needs: [build-and-test, package]
```

**Targets**:
- GitHub Packages (always)
- NuGet.org (releases only)


## Usage Scenarios

### 1. Development Workflow

#### Feature Development
```bash
# Create feature branch
git checkout -b feature/new-awesome-feature

# Make changes and test locally
./scripts/build-packages.sh Debug ./test-packages

# Push and create PR
git push origin feature/new-awesome-feature
# Open PR through GitHub UI
```

#### Code Review Process
1. Automated CI runs on PR creation
2. All tests must pass
3. Code review approval required
4. Merge to main triggers deployment pipeline

### 2. Testing Scenarios

#### Unit Testing
```bash
# Run all unit tests
dotnet test

# Run specific test project
dotnet test ThirdOpinion.Common.Aws.UnitTests/

# Generate coverage report
dotnet test --collect:"XPlat Code Coverage"
```

#### Functional Testing (Manual)
1. Go to GitHub Actions
2. Select "CI/CD Pipeline"
3. Click "Run workflow"
4. Enable "Run functional tests"
5. Click "Run workflow"

#### Local Functional Testing
```bash
# Start LocalStack
docker run --rm -p 4566:4566 -e SERVICES=cognito-idp,dynamodb,s3,sqs localstack/localstack

# Run functional tests
dotnet test ThirdOpinion.Common.FunctionalTests/ \
  -e AWS__UseLocalStack=true \
  -e AWS__LocalStackEndpoint=http://localhost:4566
```

### 3. Publishing Scenarios

#### Development Package Testing
```bash
# Manual publish for testing
# Go to GitHub Actions → CI/CD Pipeline → Run workflow
# Enable "Publish NuGet packages"
# Version format: 1.0.0-dev.YYYYMMDD.{commit-hash}
```

#### Production Release
```bash
# Create release through GitHub UI
# Version format: v1.2.3
# Automatic publishing to both GitHub Packages and NuGet.org
```

## Configuration

### Environment Variables

#### CI/CD Pipeline
```yaml
DOTNET_VERSION: '8.0.x'
SOLUTION_PATH: './ThirdOpinion.Common.sln'
```

#### Functional Tests
```yaml
AWS__UseLocalStack: true
AWS__LocalStackEndpoint: http://localhost:4566
AWS__Region: us-east-1
AWS__AccessKey: test
AWS__SecretKey: test
TestSettings__TestResourcePrefix: gh-actions
```

### Project Configuration

#### Package Metadata (Example)
```xml
<PropertyGroup>
  <PackageId>ThirdOpinion.Common.Aws.DynamoDb</PackageId>
  <Version>1.0.0</Version>
  <Authors>ThirdOpinion</Authors>
  <Description>Common DynamoDB repository and utilities</Description>
  <PackageTags>aws;dynamodb;repository</PackageTags>
  <RepositoryUrl>https://github.com/thirdopinion/common</RepositoryUrl>
  <GeneratePackageOnBuild>true</GeneratePackageOnBuild>
</PropertyGroup>
```

### Versioning Strategy

#### Release Versions
- Format: `Major.Minor.Patch` (e.g., `1.2.3`)
- Source: GitHub release tags
- Distribution: NuGet.org + GitHub Packages

#### Development Versions  
- Format: `1.0.0-dev.YYYYMMDD.{commit-hash}`
- Source: Manual workflow dispatch
- Distribution: GitHub Packages only

## Monitoring and Maintenance

### Health Checks

#### Automated Monitoring
- **Build Status**: GitHub Actions status badges
- **Test Coverage**: Coverage reports in artifacts

#### Manual Monitoring
- Monthly dependency update reviews
- Quarterly pipeline performance analysis

### Maintenance Tasks

#### Weekly
- [ ] Monitor test coverage trends
- [ ] Review dependency updates manually

#### Monthly
- [ ] Review package download metrics
- [ ] Update documentation
- [ ] Performance optimization review

#### Quarterly
- [ ] Pipeline configuration review
- [ ] Security policy updates
- [ ] Tool and action updates

## Troubleshooting

### Common Issues

#### Build Failures
```bash
# Check build logs in GitHub Actions
# Common causes:
# - Dependency conflicts
# - Test failures
# - Code analysis violations

# Local debugging:
dotnet build --verbosity diagnostic
```

#### Test Failures
```bash
# Unit test failures:
dotnet test --logger "console;verbosity=detailed"

# Functional test failures:
# - Check LocalStack health
# - Verify environment variables
# - Check AWS service configuration
```

#### Publishing Issues
```bash
# Common causes:
# - Missing API keys
# - Version conflicts
# - Package validation failures

# Debug steps:
# 1. Check repository secrets
# 2. Verify package metadata
# 3. Check NuGet.org package status
```

### Support Resources

#### Documentation
- [GitHub Actions Documentation](https://docs.github.com/en/actions)
- [.NET CLI Documentation](https://docs.microsoft.com/en-us/dotnet/core/tools/)
- [NuGet Publishing Guide](https://docs.microsoft.com/en-us/nuget/nuget-org/publish-a-package)

#### Tools
- [LocalStack Documentation](https://docs.localstack.cloud/)

## Best Practices

### Security
- ✅ Never commit secrets or API keys
- ✅ Use environment protection for production
- ✅ Regular security scanning
- ✅ Dependency vulnerability monitoring
- ✅ Principle of least privilege for tokens

### Quality
- ✅ Comprehensive test coverage
- ✅ Automated code analysis
- ✅ Consistent code formatting
- ✅ Semantic versioning
- ✅ Clear documentation

### Performance
- ✅ Parallel job execution
- ✅ Dependency caching
- ✅ Incremental builds
- ✅ Optimized Docker images
- ✅ Resource cleanup

### Maintenance
- ✅ Manual dependency updates
- ✅ Regular pipeline reviews
- ✅ Documentation updates
- ✅ Monitoring and alerting
- ✅ Disaster recovery planning

## Future Enhancements

### Planned Improvements
- [ ] Multi-target framework support (.NET 6, 8, 9)
- [ ] Automated changelog generation
- [ ] Performance benchmarking
- [ ] Integration with SonarQube
- [ ] Slack/Teams notifications

### Considerations
- [ ] Multi-repository support
- [ ] Advanced caching strategies
- [ ] Blue/green deployments
- [ ] Canary releases
- [ ] A/B testing integration

---

For questions or support, please contact the ThirdOpinion development team or create an issue in the repository.