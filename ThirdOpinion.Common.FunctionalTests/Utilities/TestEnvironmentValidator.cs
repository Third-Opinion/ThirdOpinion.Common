using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Amazon.CognitoIdentityProvider;
using Amazon.DynamoDBv2;
using Amazon.S3;
using Amazon.SQS;

namespace ThirdOpinion.Common.FunctionalTests.Utilities;

public class TestEnvironmentValidator
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<TestEnvironmentValidator> _logger;

    public TestEnvironmentValidator(IConfiguration configuration, ILogger<TestEnvironmentValidator> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ValidationResult> ValidateEnvironmentAsync()
    {
        var result = new ValidationResult();
        
        try
        {
            _logger.LogInformation("Starting test environment validation");

            // Validate configuration
            ValidateConfiguration(result);

            // Validate AWS services connectivity
            await ValidateAwsServicesAsync(result);

            // Validate test settings
            ValidateTestSettings(result);

            _logger.LogInformation("Test environment validation completed. Success: {Success}, Warnings: {Warnings}, Errors: {Errors}",
                result.IsValid, result.Warnings.Count, result.Errors.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Test environment validation failed with exception");
            result.Errors.Add($"Validation failed with exception: {ex.Message}");
        }

        return result;
    }

    private void ValidateConfiguration(ValidationResult result)
    {
        _logger.LogDebug("Validating configuration");

        // Check required configuration sections
        var requiredSections = new[] { "AWS", "TestSettings" };
        foreach (var section in requiredSections)
        {
            if (!_configuration.GetSection(section).Exists())
            {
                result.Errors.Add($"Required configuration section '{section}' is missing");
            }
        }

        // Validate AWS configuration
        var awsSection = _configuration.GetSection("AWS");
        if (awsSection.Exists())
        {
            var region = awsSection.GetValue<string>("Region");

            if (string.IsNullOrEmpty(region))
            {
                result.Warnings.Add("AWS Region not specified, using default");
            }

            _logger.LogInformation("Real AWS mode enabled");
            ValidateAwsCredentials(result);
        }

        // Validate test settings
        var testSection = _configuration.GetSection("TestSettings");
        if (testSection.Exists())
        {
            var prefix = testSection.GetValue<string>("TestResourcePrefix");
            if (string.IsNullOrEmpty(prefix))
            {
                result.Warnings.Add("TestResourcePrefix not specified, using default");
            }

            var timeout = testSection.GetValue<string>("TestTimeout");
            if (!string.IsNullOrEmpty(timeout) && !TimeSpan.TryParse(timeout, out _))
            {
                result.Errors.Add($"Invalid TestTimeout format: {timeout}");
            }

            var retryDelay = testSection.GetValue<string>("RetryDelay");
            if (!string.IsNullOrEmpty(retryDelay) && !TimeSpan.TryParse(retryDelay, out _))
            {
                result.Errors.Add($"Invalid RetryDelay format: {retryDelay}");
            }
        }
    }

    private void ValidateAwsCredentials(ValidationResult result)
    {
        _logger.LogDebug("Validating AWS credentials");

        var hasEnvironmentCredentials = 
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID")) &&
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY"));

        var hasProfileCredentials = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_PROFILE"));

        var hasSharedCredentials = File.Exists(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 
            ".aws", 
            "credentials"));

        var hasInstanceProfile = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_CONTAINER_CREDENTIALS_RELATIVE_URI")) ||
                                !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_EC2_METADATA_DISABLED"));

        if (!hasEnvironmentCredentials && !hasProfileCredentials && !hasSharedCredentials && !hasInstanceProfile)
        {
            result.Errors.Add("No AWS credentials found. Configure AWS credentials using AWS CLI, environment variables, or IAM role.");
        }
        else
        {
            _logger.LogInformation("AWS credentials found");
        }
    }

    private async Task ValidateAwsServicesAsync(ValidationResult result)
    {
        _logger.LogDebug("Validating AWS services connectivity");

        var serviceProvider = TestCollectionSetupHelper.BuildTestServiceProvider(_configuration);

        try
        {
            // Test Cognito connectivity
            await ValidateCognitoAsync(serviceProvider, result);

            // Test DynamoDB connectivity
            await ValidateDynamoDbAsync(serviceProvider, result);

            // Test S3 connectivity
            await ValidateS3Async(serviceProvider, result);

            // Test SQS connectivity
            await ValidateSqsAsync(serviceProvider, result);
        }
        catch (Exception ex)
        {
            result.Errors.Add($"AWS services validation failed: {ex.Message}");
            _logger.LogError(ex, "AWS services validation failed");
        }
    }

    private async Task ValidateCognitoAsync(IServiceProvider serviceProvider, ValidationResult result)
    {
        try
        {
            var cognitoClient = serviceProvider.GetRequiredService(typeof(IAmazonCognitoIdentityProvider)) as IAmazonCognitoIdentityProvider;
            var response = await cognitoClient.ListUserPoolsAsync(new Amazon.CognitoIdentityProvider.Model.ListUserPoolsRequest
            {
                MaxResults = 1
            });
            
            result.ServiceValidations["Cognito"] = "✓ Connected";
            _logger.LogDebug("Cognito connectivity validated");
        }
        catch (Exception ex)
        {
            result.ServiceValidations["Cognito"] = $"✗ Failed: {ex.Message}";
            result.Warnings.Add($"Cognito connectivity test failed: {ex.Message}");
            _logger.LogWarning(ex, "Cognito connectivity test failed");
        }
    }

    private async Task ValidateDynamoDbAsync(IServiceProvider serviceProvider, ValidationResult result)
    {
        try
        {
            var dynamoClient = serviceProvider.GetRequiredService(typeof(IAmazonDynamoDB)) as IAmazonDynamoDB;
            var response = await dynamoClient.ListTablesAsync(new Amazon.DynamoDBv2.Model.ListTablesRequest
            {
                Limit = 1
            });
            
            result.ServiceValidations["DynamoDB"] = "✓ Connected";
            _logger.LogDebug("DynamoDB connectivity validated");
        }
        catch (Exception ex)
        {
            result.ServiceValidations["DynamoDB"] = $"✗ Failed: {ex.Message}";
            result.Warnings.Add($"DynamoDB connectivity test failed: {ex.Message}");
            _logger.LogWarning(ex, "DynamoDB connectivity test failed");
        }
    }

    private async Task ValidateS3Async(IServiceProvider serviceProvider, ValidationResult result)
    {
        try
        {
            var s3Client = serviceProvider.GetRequiredService(typeof(IAmazonS3)) as IAmazonS3;
            var response = await s3Client.ListBucketsAsync();
            
            result.ServiceValidations["S3"] = "✓ Connected";
            _logger.LogDebug("S3 connectivity validated");
        }
        catch (Exception ex)
        {
            result.ServiceValidations["S3"] = $"✗ Failed: {ex.Message}";
            result.Warnings.Add($"S3 connectivity test failed: {ex.Message}");
            _logger.LogWarning(ex, "S3 connectivity test failed");
        }
    }

    private async Task ValidateSqsAsync(IServiceProvider serviceProvider, ValidationResult result)
    {
        try
        {
            var sqsClient = serviceProvider.GetRequiredService(typeof(IAmazonSQS)) as IAmazonSQS;
            var response = await sqsClient.ListQueuesAsync(new Amazon.SQS.Model.ListQueuesRequest
            {
                MaxResults = 1
            });
            
            result.ServiceValidations["SQS"] = "✓ Connected";
            _logger.LogDebug("SQS connectivity validated");
        }
        catch (Exception ex)
        {
            result.ServiceValidations["SQS"] = $"✗ Failed: {ex.Message}";
            result.Warnings.Add($"SQS connectivity test failed: {ex.Message}");
            _logger.LogWarning(ex, "SQS connectivity test failed");
        }
    }

    private void ValidateTestSettings(ValidationResult result)
    {
        _logger.LogDebug("Validating test settings");

        var testSettings = _configuration.GetSection("TestSettings");
        
        // Validate timeout settings
        var timeout = TestCollectionSetupHelper.GetTestTimeout(_configuration);
        if (timeout.TotalSeconds < 30)
        {
            result.Warnings.Add("Test timeout is very short, tests may fail due to AWS service latency");
        }
        else if (timeout.TotalMinutes > 30)
        {
            result.Warnings.Add("Test timeout is very long, consider reducing for faster feedback");
        }

        // Validate retry settings
        var maxRetries = TestCollectionSetupHelper.GetMaxRetryAttempts(_configuration);
        if (maxRetries < 1)
        {
            result.Errors.Add("MaxRetryAttempts must be at least 1");
        }
        else if (maxRetries > 10)
        {
            result.Warnings.Add("MaxRetryAttempts is very high, tests may take a long time");
        }

        // Validate parallel execution settings
        var parallelExecution = _configuration.GetValue<bool>("TestSettings:ParallelExecution");
        
        if (parallelExecution)
        {
            result.Warnings.Add("Parallel execution with real AWS services may cause resource conflicts");
        }

        // Check if running in CI
        if (TestCollectionSetupHelper.IsRunningInCI())
        {
            result.Warnings.Add("Running in CI environment - ensure AWS credentials are properly configured");
        }
    }
}

public class ValidationResult
{
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();
    public Dictionary<string, string> ServiceValidations { get; } = new();
    
    public bool IsValid => Errors.Count == 0;
    
    public string GetSummary()
    {
        var summary = new List<string>();
        
        if (IsValid)
        {
            summary.Add("✓ Test environment validation passed");
        }
        else
        {
            summary.Add("✗ Test environment validation failed");
        }

        if (Errors.Any())
        {
            summary.Add($"\nErrors ({Errors.Count}):");
            summary.AddRange(Errors.Select(e => $"  • {e}"));
        }

        if (Warnings.Any())
        {
            summary.Add($"\nWarnings ({Warnings.Count}):");
            summary.AddRange(Warnings.Select(w => $"  • {w}"));
        }

        if (ServiceValidations.Any())
        {
            summary.Add("\nService Connectivity:");
            summary.AddRange(ServiceValidations.Select(kv => $"  • {kv.Key}: {kv.Value}"));
        }

        return string.Join("\n", summary);
    }
}