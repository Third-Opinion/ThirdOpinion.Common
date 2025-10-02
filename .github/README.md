# ThirdOpinion.Common CI/CD Pipeline

This repository includes a comprehensive CI/CD pipeline for building, testing, and publishing the ThirdOpinion.Common NuGet packages.

## Workflows

### 1. CI/CD Pipeline (`ci-cd.yml`)

The main CI/CD workflow that handles:

- **Automatic Triggers:**
  - Push to `main`, `master`, or `develop` branches
  - Pull requests to `main` or `master`
  - GitHub releases

- **Manual Triggers:**
  - Workflow dispatch with options to run functional tests and publish packages

#### Jobs:

1. **Build and Test**
   - Builds the entire solution
   - Runs unit tests for both AWS and Common projects
   - Generates code coverage reports
   - Uploads test results and coverage as artifacts

2. **Functional Tests** (manual trigger or workflow dispatch)
   - Spins up LocalStack for AWS service emulation
   - Runs comprehensive functional tests against AWS services
   - Only runs when explicitly requested

3. **Package Creation**
   - Creates NuGet packages for all library projects
   - Versions packages based on release tags or development builds
   - Uploads packages as artifacts

4. **Publishing**
   - Publishes to AWS CodeArtifact for all builds (main, develop, releases)
   - Requires AWS CodeArtifact configuration secrets

## Setup Requirements

### Repository Secrets

Add these secrets to your GitHub repository:

```
# AWS CodeArtifact Configuration
AWS_CODEARTIFACT_ROLE_ARN           # IAM role ARN for CodeArtifact access
AWS_CODEARTIFACT_DOMAIN             # CodeArtifact domain name
AWS_CODEARTIFACT_DOMAIN_OWNER       # AWS account ID that owns the domain
AWS_CODEARTIFACT_REPOSITORY         # CodeArtifact repository name

# Functional Tests (optional)
AWS_FUNCTIONAL_TEST_ROLE_ARN        # IAM role ARN for running functional tests
```

### Environment Protection

The workflow uses a `production` environment for publishing. Configure this in your repository settings:

1. Go to **Settings** → **Environments**
2. Create `production` environment
3. Add protection rules (e.g., required reviewers)

## Package Structure

The workflow will publish this NuGet package to AWS CodeArtifact:

- `ThirdOpinion.Common` - Complete package including AWS services integration (S3, DynamoDB, SQS, Cognito), FHIR R4 healthcare integration, and utility functions

### Installing from CodeArtifact

To consume the package from AWS CodeArtifact:

1. Configure AWS CLI with appropriate credentials
2. Add CodeArtifact as a NuGet source:
   ```bash
   aws codeartifact login --tool dotnet --domain YOUR_DOMAIN --repository YOUR_REPOSITORY
   ```
3. Install the package:
   ```bash
   dotnet add package ThirdOpinion.Common
   ```

## Usage Examples

### Running Functional Tests Manually

1. Go to **Actions** tab in GitHub
2. Select **CI/CD Pipeline** workflow
3. Click **Run workflow**
4. Check **Run functional tests**
5. Click **Run workflow**

### Publishing Development Packages

1. Go to **Actions** tab in GitHub
2. Select **CI/CD Pipeline** workflow
3. Click **Run workflow**
4. Check **Publish NuGet packages**
5. Click **Run workflow**

Development packages will be versioned as: `2.0.0-beta.{build-number}` for develop branch

### Publishing Release Packages

1. Create a GitHub release with a tag (e.g., `v2.1.0`)
2. The workflow will automatically:
   - Build and test the code
   - Create packages with the release version
   - Publish to AWS CodeArtifact

## Local Development

### Running Tests Locally

```bash
# Unit tests only
dotnet test ThirdOpinion.Common.Aws.UnitTests/
dotnet test ThirdOpinion.Common.UnitTests/

# Build packages locally
dotnet pack --configuration Release --output ./packages/
```


## Monitoring and Maintenance

### Code Quality

- All PRs require passing tests
- Code coverage reports are generated for each build

## Troubleshooting

### Common Issues

1. **Functional tests fail**: Ensure LocalStack is properly started and health checks pass
2. **Package publishing fails**: Check that secrets are properly configured
3. **Version conflicts**: Ensure package versions are properly incremented
4. **Test timeouts**: Functional tests may need longer timeouts in CI environment

### Debugging

- Check the **Actions** tab for detailed logs
- Download test results and coverage artifacts