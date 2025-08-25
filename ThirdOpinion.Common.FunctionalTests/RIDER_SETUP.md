# Rider Run Configurations for Functional Tests

## Overview
This document describes the Rider run configurations available for running AWS functional tests.

## Prerequisites
1. **AWS SSO Login**: Before running tests, ensure you're logged into AWS SSO:
   ```bash
   aws sso login --profile to-dev-admin
   ```

2. **AWS Profile**: All configurations are pre-configured with the `AWS_PROFILE=to-dev-admin` environment variable.

## Available Run Configurations

### 1. Functional Tests (All)
- **Purpose**: Runs all functional tests across all AWS services
- **Test Count**: 34 tests
- **Estimated Duration**: 1-2 minutes

### 2. Functional Tests (S3 Only)
- **Purpose**: Runs only S3-related functional tests
- **Filter**: `FullyQualifiedName~S3FunctionalTests`
- **Test Count**: 9 tests
- **Tests Include**: Bucket creation, object operations, multipart upload, presigned URLs

### 3. Functional Tests (Cognito Only)
- **Purpose**: Runs only Cognito-related functional tests
- **Filter**: `FullyQualifiedName~CognitoFunctionalTests`
- **Test Count**: 5 tests
- **Tests Include**: User pool creation, user management, authentication, token refresh

### 4. Functional Tests (DynamoDB Only)
- **Purpose**: Runs only DynamoDB-related functional tests
- **Filter**: `FullyQualifiedName~DynamoDbFunctionalTests`
- **Test Count**: 7 tests
- **Tests Include**: Table operations, CRUD, batch operations, transactions, GSI queries

### 5. Functional Tests (SQS Only)
- **Purpose**: Runs only SQS-related functional tests
- **Filter**: `FullyQualifiedName~SqsFunctionalTests`
- **Test Count**: 9 tests
- **Tests Include**: Queue operations, message handling, FIFO queues, long polling

### 6. Functional Tests (Cross-Service)
- **Purpose**: Runs integration tests that span multiple AWS services
- **Filter**: `FullyQualifiedName~CrossServiceIntegrationTests`
- **Test Count**: 3 tests
- **Tests Include**: User registration workflow, document processing pipeline, data backup/restore

### 7. Functional Tests (xUnit Runner)
- **Purpose**: Uses the .NET Test runner directly (alternative runner)
- **Benefits**: Better integration with Rider's test explorer window

## How to Use in Rider

1. **Open Run Configurations**: 
   - Use keyboard shortcut: `Shift + Alt + F10` (Windows/Linux) or `Ctrl + Alt + R` (macOS)
   - Or click the dropdown next to the Run button in the toolbar

2. **Select Configuration**: Choose the appropriate configuration based on what you want to test

3. **Run Tests**: 
   - Click the green Run button
   - Or use keyboard shortcut: `Shift + F10` (Windows/Linux) or `Ctrl + R` (macOS)

4. **Debug Tests**:
   - Click the Debug button
   - Or use keyboard shortcut: `Shift + F9` (Windows/Linux) or `Ctrl + D` (macOS)

## Environment Variables

All configurations include the following environment variables:
- `AWS_PROFILE=to-dev-admin` - AWS SSO profile for test environment
- `ASPNETCORE_ENVIRONMENT=Test` - Sets the ASP.NET Core environment
- `DOTNET_ENVIRONMENT=Test` - Sets the .NET environment (xUnit Runner only)

## Test Execution Notes

### Resource Cleanup
All tests implement proper cleanup in their `Dispose()` methods to ensure AWS resources are deleted after test completion.

### Test Isolation
Each test creates uniquely named resources with timestamps to avoid conflicts when running tests in parallel or repeatedly.

### Cost Considerations
These tests create real AWS resources. While they clean up after themselves, there may be minimal costs associated with:
- S3 storage (during test execution)
- DynamoDB read/write capacity
- SQS message operations
- Cognito user pool operations

### Parallel Execution
Tests are designed to run in parallel safely. Each test uses unique resource names to avoid conflicts.

## Troubleshooting

### AWS Credentials Not Found
If tests fail with credential errors:
1. Verify SSO login: `aws sso login --profile to-dev-admin`
2. Check AWS CLI configuration: `aws configure list --profile to-dev-admin`
3. Ensure the profile name matches in both AWS config and environment variable

### Test Failures Due to Existing Resources
If tests fail due to existing resources:
1. Resources should auto-cleanup, but if interrupted, manual cleanup may be needed
2. Check AWS Console for orphaned resources with pattern: `*-test-*` or `functest-*`

### Debugging Specific Tests
To debug a single test:
1. Open the test file in Rider
2. Click the gutter icon next to the test method
3. Select "Debug" from the context menu

## Customizing Configurations

To modify or create new configurations:
1. Navigate to: Run → Edit Configurations
2. Modify existing configurations or create new ones
3. Key settings to adjust:
   - **Program parameters**: Add test filters using `--filter "expression"`
   - **Environment variables**: Add or modify AWS settings
   - **Working directory**: Ensure it points to the test project

## Command Line Equivalent

These configurations can also be run from the command line:

```bash
# All tests
export AWS_PROFILE=to-dev-admin
dotnet test ThirdOpinion.Common.FunctionalTests/ThirdOpinion.Common.FunctionalTests.csproj

# Filtered tests (e.g., S3 only)
dotnet test ThirdOpinion.Common.FunctionalTests/ThirdOpinion.Common.FunctionalTests.csproj \
  --filter "FullyQualifiedName~S3FunctionalTests"
```