# ThirdOpinion.Common.FunctionalTests

**Status: ✅ Functional tests are now working and run against AWS Dev Account (615299752206)**

This project provides comprehensive functional and integration tests for ThirdOpinion.Common AWS services libraries.
Tests run against real AWS services using the development account.

## Overview

This test project provides end-to-end testing for:

- **AWS Cognito** - User authentication and management
- **AWS DynamoDB** - NoSQL database operations
- **AWS S3** - Object storage operations
- **AWS SQS** - Message queue operations
- **Cross-Service Integration** - Multi-service workflows

## Project Structure

```
ThirdOpinion.Common.FunctionalTests/
├── Infrastructure/              # Base test classes and setup
│   ├── BaseIntegrationTest.cs   # Base class for all functional tests
│   ├── TestDataBuilder.cs      # Test data generation utilities
│   └── AwsResourceCleaner.cs    # Resource cleanup utilities
├── Tests/                       # Test implementations
│   ├── CognitoFunctionalTests.cs
│   ├── DynamoDbFunctionalTests.cs
│   ├── S3FunctionalTests.cs
│   ├── SqsFunctionalTests.cs
│   └── CrossServiceIntegrationTests.cs
├── Utilities/                   # Helper utilities
│   ├── TestCollectionSetupHelper.cs
│   ├── TestEnvironmentValidator.cs
│   └── TestDataGenerators.cs
├── Configuration Files/
│   ├── appsettings.Test.json    # LocalStack configuration
│   ├── appsettings.Integration.json # CI/CD configuration
│   └── appsettings.Staging.json # Staging environment
└── .github/workflows/           # CI/CD pipeline
    └── functional-tests.yml
```

## Getting Started

### Prerequisites

- .NET 8.0 SDK
- AWS CLI configured with appropriate credentials
- Access to AWS Dev Account (615299752206)

### Local Development Setup

1. **Clone and build:**
   ```bash
   git clone <repository-url>
   cd ThirdOpinion.Common
   dotnet restore
   dotnet build
   ```

2. **Configure AWS SSO credentials (required):**
   ```bash
   # Configure SSO profile if not already done
   aws configure sso --profile to-dev-admin
   
   # Login to AWS SSO
   aws sso login --profile to-dev-admin
   
   # Set environment variable to use the profile
   export AWS_PROFILE=to-dev-admin
   
   # Verify access to dev account (should show account 615299752206)
   aws sts get-caller-identity
   
   # Alternative: Use the provided helper script
   ./run-tests.sh
   ```

   **Note:** AWS access keys (AWS_ACCESS_KEY_ID/AWS_SECRET_ACCESS_KEY) are not supported. This project requires SSO
   authentication only.

3. **Run functional tests:**
   ```bash
   # Using the helper script (automatically sets AWS_PROFILE)
   ./run-tests.sh              # Run all tests
   ./run-tests.sh s3           # Run S3 tests only
   ./run-tests.sh cognito      # Run Cognito tests only
   ./run-tests.sh dynamodb     # Run DynamoDB tests only
   ./run-tests.sh sqs          # Run SQS tests only
   
   # Or manually with dotnet test (ensure AWS_PROFILE is set)
   export AWS_PROFILE=to-dev-admin
   dotnet test ThirdOpinion.Common.FunctionalTests
   
   # Run specific test category
   dotnet test --filter "Category=Cognito"
   dotnet test --filter "Category=DynamoDB"
   dotnet test --filter "Category=S3"
   dotnet test --filter "Category=SQS"
   dotnet test --filter "Category=CrossService"
   ```

### Important Notes for Local Development

- **AWS Account**: All tests run against AWS Dev Account (615299752206)
- **Resource Cleanup**: Tests automatically clean up resources they create
- **Resource Prefix**: Local tests use "functest" prefix by default
- **Cost Management**: Tests use minimal AWS resources to keep costs low
- **Security**: Never commit AWS credentials - always use profiles or environment variables

### Authentication Requirements

The tests require AWS SSO authentication:

1. **AWS_PROFILE environment variable** (default: to-dev-admin)
2. **SSO profile configured in ~/.aws/config**

**Important:** AWS access keys (AWS_ACCESS_KEY_ID, AWS_SECRET_ACCESS_KEY) are explicitly rejected. Only SSO
authentication is supported for enhanced security.

## Configuration

### Test Configuration

All tests now run against the AWS Dev Account with the following settings:

```json
{
  "AWS": {
    "UseLocalStack": false,
    "Region": "us-east-2",
    "AccountId": "615299752206"
  },
  "TestSettings": {
    "TestResourcePrefix": "functest",
    "TestTimeout": "00:05:00",
    "CleanupResources": true,
    "ParallelExecution": false,
    "MaxRetryAttempts": 3,
    "RetryDelay": "00:00:02"
  }
}
```

### Authentication Configuration

#### Local Development

- Uses AWS_PROFILE environment variable (default: to-dev-admin)
- Requires SSO profile configured in ~/.aws/config
- Supports multiple SSO profiles for different environments
- AWS access keys are not supported - SSO only

#### CI/CD (GitHub Actions)

- Uses IAM role assumption with OIDC tokens
- Role: `arn:aws:iam::615299752206:role/dev-cdk-role-ue2-github-actions`
- No long-lived credentials stored in secrets

## Test Categories

### Cognito Tests

- User pool creation and management
- User registration and authentication
- Token refresh workflows
- User listing and management operations

### DynamoDB Tests

- Table creation and management
- CRUD operations (Create, Read, Update, Delete)
- Batch operations and transactions
- Global Secondary Index (GSI) queries
- Scan operations with filters
- Conditional updates

### S3 Tests

- Bucket creation and management
- Object upload, download, and metadata
- Multipart uploads for large files
- Presigned URL generation
- Object copying and deletion
- Binary and text content handling

### SQS Tests

- Queue creation and configuration
- Message sending and receiving
- Batch operations
- Message visibility and deletion
- Long polling
- FIFO queue operations (when supported)

### Cross-Service Integration Tests

- **User Registration Workflow**: Cognito → DynamoDB → S3 → SQS
- **Document Processing Pipeline**: Authentication → Upload → Processing → Notification
- **Data Backup and Restore**: DynamoDB → S3 → SQS → DynamoDB

## Test Data Generation

The project uses Bogus library for realistic test data generation:

```csharp
// Generate test user
var (email, password, attributes) = TestDataBuilder.CreateTestUser();

// Generate DynamoDB test data
var data = TestDataBuilder.CreateDynamoDbTestData();

// Generate binary test data
var binaryData = TestDataBuilder.CreateBinaryTestData(1024);
```

## Resource Management

### Automatic Cleanup

- All tests automatically clean up created resources
- Base test class handles cleanup in `CleanupTestResourcesAsync()`
- Failed tests still attempt cleanup to prevent resource leaks

### Resource Naming

- Test resources use unique, timestamped names
- Format: `{prefix}-{testname}-{timestamp}-{random}`
- Easy identification and cleanup of orphaned resources

### Cost Management

- LocalStack is free and recommended for development
- Real AWS tests use minimal resources
- Automatic cleanup prevents accumulating costs
- Short retention periods for test data

## CI/CD Integration

### GitHub Actions Workflow

The project includes a comprehensive GitHub Actions workflow:

1. **Matrix Testing**: Runs test suites in parallel
2. **LocalStack Integration**: Uses containerized LocalStack
3. **Real AWS Testing**: Optional testing against real AWS services
4. **Test Results**: Publishes test results and coverage reports
5. **Security Scanning**: Checks for potential secrets in test files

### Running in CI/CD

```yaml
# Functional tests (LocalStack)
- name: Run functional tests
  run: dotnet test ThirdOpinion.Common.FunctionalTests
  env:
    AWS__UseLocalStack: true
    AWS__LocalStackEndpoint: http://localhost:4566

# Integration tests (Real AWS with OIDC)
- name: Configure AWS Credentials
  uses: aws-actions/configure-aws-credentials@v1
  with:
    role-to-assume: arn:aws:iam::615299752206:role/dev-cdk-role-ue2-github-actions
    role-session-name: GitHubActions
    aws-region: us-east-2

- name: Run integration tests
  run: dotnet test ThirdOpinion.Common.FunctionalTests
  env:
    AWS__UseLocalStack: false
```

## Troubleshooting

### Common Issues

1. **LocalStack not responding**
   ```bash
   # Check LocalStack health
   curl http://localhost:4566/_localstack/health
   
   # Restart LocalStack
   docker restart <localstack-container>
   ```

2. **AWS credential issues**
   ```bash
   # Verify SSO login
   aws sts get-caller-identity --profile to-dev-admin
   
   # Re-login if expired
   aws sso login --profile to-dev-admin
   
   # Ensure AWS_PROFILE is set
   export AWS_PROFILE=to-dev-admin
   
   # Check for rejected access keys
   unset AWS_ACCESS_KEY_ID
   unset AWS_SECRET_ACCESS_KEY
   ```

3. **Test timeouts**
    - Increase `TestSettings:TestTimeout` in configuration
    - Check AWS service availability
    - Verify network connectivity

4. **Resource cleanup failures**
    - Check AWS permissions
    - Review CloudTrail logs for errors
    - Manually clean up orphaned resources

### Debug Mode

Enable verbose logging for troubleshooting:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "ThirdOpinion": "Trace"
    }
  }
}
```

## Contributing

### Adding New Tests

1. **Create test class** inheriting from `BaseIntegrationTest`
2. **Add appropriate test category** attribute
3. **Implement cleanup** in `CleanupTestResourcesAsync()`
4. **Use test data builders** for consistent data generation
5. **Follow naming conventions** for test resources

### Test Guidelines

- Tests should be independent and idempotent
- Use descriptive test names following pattern: `Method_Scenario_ExpectedResult`
- Always clean up created resources
- Use appropriate assertions with Shouldly
- Handle AWS service eventual consistency
- Include both positive and negative test cases

### Performance Considerations

- Minimize AWS API calls where possible
- Use batch operations when available
- Implement retry logic for flaky operations
- Monitor test execution time and optimize slow tests
- Use appropriate resource sizes for testing

## Security

### Credentials Management

- Never commit real AWS credentials
- Use environment variables or AWS profiles
- Leverage IAM roles in CI/CD environments
- Rotate test credentials regularly

### Test Data Security

- Use fake data for all tests
- Avoid PII in test datasets
- Ensure test resources are properly isolated
- Implement least-privilege IAM policies for test users

## Monitoring and Metrics

### Test Metrics

- Test execution time per category
- Success/failure rates
- Resource creation/cleanup success
- AWS API call patterns

### Alerting

- Failed test notifications
- Resource cleanup failures
- Unusual test execution patterns
- AWS cost anomalies

## Support

For questions or issues:

1. Check the troubleshooting section above
2. Review test logs and error messages
3. Consult AWS service documentation
4. Open an issue with detailed error information

## License

This project is part of the ThirdOpinion.Common library and follows the same licensing terms.