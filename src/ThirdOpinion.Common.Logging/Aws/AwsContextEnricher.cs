using Amazon.Util;
using Serilog.Core;
using Serilog.Events;

namespace ThirdOpinion.Common.Logging.Aws;

/// <summary>
/// Enricher that adds AWS-specific context information to log events
/// </summary>
public class AwsContextEnricher : ILogEventEnricher
{
    private readonly string? _instanceId;
    private readonly string? _instanceType;
    private readonly string? _availabilityZone;
    private readonly string? _region;
    private readonly string? _lambdaFunctionName;
    private readonly string? _lambdaFunctionVersion;

    public AwsContextEnricher()
    {
        // Try to get EC2 instance metadata
        try
        {
            if (EC2InstanceMetadata.IsIMDSEnabled)
            {
                _instanceId = EC2InstanceMetadata.InstanceId;
                _instanceType = EC2InstanceMetadata.InstanceType;
                _availabilityZone = EC2InstanceMetadata.AvailabilityZone;
                _region = EC2InstanceMetadata.Region?.SystemName;
            }
        }
        catch
        {
            // Not running on EC2, ignore
        }

        // Try to get Lambda context from environment variables
        _lambdaFunctionName = Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME");
        _lambdaFunctionVersion = Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_VERSION");

        // Get region from environment if not already set
        _region ??= Environment.GetEnvironmentVariable("AWS_REGION") ?? Environment.GetEnvironmentVariable("AWS_DEFAULT_REGION");
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (!string.IsNullOrEmpty(_instanceId))
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("EC2InstanceId", _instanceId));
        }

        if (!string.IsNullOrEmpty(_instanceType))
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("EC2InstanceType", _instanceType));
        }

        if (!string.IsNullOrEmpty(_availabilityZone))
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("AvailabilityZone", _availabilityZone));
        }

        if (!string.IsNullOrEmpty(_region))
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("AWSRegion", _region));
        }

        if (!string.IsNullOrEmpty(_lambdaFunctionName))
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("LambdaFunctionName", _lambdaFunctionName));
        }

        if (!string.IsNullOrEmpty(_lambdaFunctionVersion))
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("LambdaFunctionVersion", _lambdaFunctionVersion));
        }

        // Add execution environment
        var executionEnvironment = DetermineExecutionEnvironment();
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ExecutionEnvironment", executionEnvironment));
    }

    private string DetermineExecutionEnvironment()
    {
        if (!string.IsNullOrEmpty(_lambdaFunctionName))
            return "Lambda";

        if (!string.IsNullOrEmpty(_instanceId))
            return "EC2";

        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ECS_CONTAINER_METADATA_URI")))
            return "ECS";

        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST")))
            return "EKS";

        return "Local";
    }
}