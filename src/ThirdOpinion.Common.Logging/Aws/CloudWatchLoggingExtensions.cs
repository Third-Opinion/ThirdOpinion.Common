using Amazon;
using Amazon.CloudWatchLogs;
using Amazon.Util;
using AWS.Logger;
using AWS.Logger.SeriLog;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;

namespace ThirdOpinion.Common.Logging.Aws;

/// <summary>
/// Extension methods for configuring AWS CloudWatch Logs with Serilog
/// </summary>
public static class CloudWatchLoggingExtensions
{
    /// <summary>
    /// Add AWS CloudWatch Logs sink to Serilog configuration
    /// </summary>
    /// <param name="loggerConfiguration">Logger configuration to extend</param>
    /// <param name="configuration">Application configuration</param>
    /// <param name="applicationName">Application name for log group naming</param>
    /// <returns>Modified logger configuration</returns>
    public static LoggerConfiguration AddCloudWatchLogs(
        this LoggerConfiguration loggerConfiguration,
        IConfiguration configuration,
        string applicationName)
    {
        var cloudWatchConfig = configuration.GetSection("Logging:CloudWatch");

        if (!cloudWatchConfig.GetValue<bool>("Enabled", false))
        {
            return loggerConfiguration;
        }

        var logGroupName = cloudWatchConfig.GetValue<string>("LogGroup")
            ?? $"/aws/application/{applicationName}";

        var region = cloudWatchConfig.GetValue<string>("Region")
            ?? Environment.GetEnvironmentVariable("AWS_REGION")
            ?? Environment.GetEnvironmentVariable("AWS_DEFAULT_REGION")
            ?? "us-east-1";

        var retentionDays = cloudWatchConfig.GetValue<int>("RetentionDays", 30);
        var minimumLevel = cloudWatchConfig.GetValue<LogEventLevel>("MinimumLevel", LogEventLevel.Information);

        var options = new AWSLoggerConfig
        {
            Region = region,
            LogGroup = logGroupName,
            BatchPushInterval = TimeSpan.FromSeconds(cloudWatchConfig.GetValue<int>("BatchIntervalSeconds", 5)),
            BatchSizeInBytes = cloudWatchConfig.GetValue<int>("BatchSizeInBytes", 256000),
            MaxQueuedMessages = cloudWatchConfig.GetValue<int>("MaxQueuedMessages", 10000),
            LogStreamNameSuffix = GetLogStreamSuffix()
        };

        // Configure credentials if provided
        var accessKeyId = cloudWatchConfig.GetValue<string>("AccessKeyId");
        var secretAccessKey = cloudWatchConfig.GetValue<string>("SecretAccessKey");

        if (!string.IsNullOrEmpty(accessKeyId) && !string.IsNullOrEmpty(secretAccessKey))
        {
            options.Credentials = new Amazon.Runtime.BasicAWSCredentials(accessKeyId, secretAccessKey);
        }

        // Create log group if needed
        var createLogGroup = cloudWatchConfig.GetValue<bool>("CreateLogGroup", true);
        if (createLogGroup)
        {
            EnsureLogGroupExists(options, logGroupName, retentionDays).GetAwaiter().GetResult();
        }

        return loggerConfiguration.WriteTo.AWSSeriLog(
            configuration: options,
            restrictedToMinimumLevel: minimumLevel);
    }

    /// <summary>
    /// Configure complete AWS logging with enrichers and CloudWatch
    /// </summary>
    public static LoggerConfiguration ConfigureAwsLogging(
        this LoggerConfiguration loggerConfiguration,
        IConfiguration configuration,
        string applicationName)
    {
        return loggerConfiguration
            .Enrich.With<AwsContextEnricher>()
            .AddCloudWatchLogs(configuration, applicationName);
    }

    private static string GetLogStreamSuffix()
    {
        // Generate unique suffix for log stream
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        var instanceId = EC2InstanceMetadata.InstanceId ?? Environment.MachineName;
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd");

        return $"{environment}/{instanceId}/{timestamp}";
    }

    private static async Task EnsureLogGroupExists(
        AWSLoggerConfig config,
        string logGroupName,
        int retentionDays)
    {
        try
        {
            IAmazonCloudWatchLogs client;
            if (config.Credentials != null)
            {
                client = new AmazonCloudWatchLogsClient(config.Credentials, RegionEndpoint.GetBySystemName(config.Region));
            }
            else
            {
                client = new AmazonCloudWatchLogsClient(RegionEndpoint.GetBySystemName(config.Region));
            }

            using (client)
            {
                // Check if log group exists
                var describeRequest = new Amazon.CloudWatchLogs.Model.DescribeLogGroupsRequest
                {
                    LogGroupNamePrefix = logGroupName
                };

                var response = await client.DescribeLogGroupsAsync(describeRequest);
                var logGroup = response.LogGroups.FirstOrDefault(lg => lg.LogGroupName == logGroupName);

                if (logGroup == null)
                {
                    // Create log group
                    var createRequest = new Amazon.CloudWatchLogs.Model.CreateLogGroupRequest
                    {
                        LogGroupName = logGroupName
                    };
                    await client.CreateLogGroupAsync(createRequest);

                    // Set retention policy
                    var retentionRequest = new Amazon.CloudWatchLogs.Model.PutRetentionPolicyRequest
                    {
                        LogGroupName = logGroupName,
                        RetentionInDays = retentionDays
                    };
                    await client.PutRetentionPolicyAsync(retentionRequest);
                }
                else if (logGroup.RetentionInDays != retentionDays)
                {
                    // Update retention policy
                    var retentionRequest = new Amazon.CloudWatchLogs.Model.PutRetentionPolicyRequest
                    {
                        LogGroupName = logGroupName,
                        RetentionInDays = retentionDays
                    };
                    await client.PutRetentionPolicyAsync(retentionRequest);
                }
            }
        }
        catch
        {
            // Ignore errors in log group creation
        }
    }
}