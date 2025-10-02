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
   - Publishes to GitHub Packages (always)
   - Publishes to NuGet.org (releases only)
   - Requires `NUGET_API_KEY` secret for NuGet.org

## Setup Requirements

### Repository Secrets

Add these secrets to your GitHub repository:

```
NUGET_API_KEY          # API key for publishing to NuGet.org
```

### Environment Protection

The workflow uses a `production` environment for publishing. Configure this in your repository settings:

1. Go to **Settings** → **Environments**
2. Create `production` environment
3. Add protection rules (e.g., required reviewers)

## Package Structure

The workflow will publish this NuGet package:

- `ThirdOpinion.Common` - Complete package including AWS services integration (S3, DynamoDB, SQS, Cognito), FHIR R4 healthcare integration, and utility functions

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

Development packages will be versioned as: `1.0.0-dev.YYYYMMDD.{commit-hash}`

### Publishing Release Packages

1. Create a GitHub release with a tag (e.g., `v1.2.3`)
2. The workflow will automatically:
   - Build and test the code
   - Create packages with the release version
   - Publish to both GitHub Packages and NuGet.org

## Local Development

### Running Tests Locally

```bash
# Unit tests only
dotnet test ThirdOpinion.Common.Aws.UnitTests/
dotnet test ThirdOpinion.Common.UnitTests/

# Build packages locally
dotnet pack --configuration Release --output ./packages/
```

### Functional Tests Setup

To run functional tests locally, you'll need LocalStack:

```bash
# Using Docker
docker run --rm -it -p 4566:4566 -e SERVICES=cognito-idp,dynamodb,s3,sqs localstack/localstack

# Run functional tests
dotnet test ThirdOpinion.Common.FunctionalTests/ \
  -e AWS__UseLocalStack=true \
  -e AWS__LocalStackEndpoint=http://localhost:4566
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