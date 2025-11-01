using Amazon;
using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;
using Amazon.Runtime;
using Amazon.Util;
using AWS.Logger;
using AWS.Logger.SeriLog;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;

namespace ThirdOpinion.Common.Logging.Aws;

/// <summary>
///     Extension methods for configuring AWS CloudWatch Logs with Serilog
/// </summary>
public static class CloudWatchLoggingExtensions
{
    /// <summary>
    ///     Add AWS CloudWatch Logs sink to Serilog configuration
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
        IConfigurationSection cloudWatchConfig = configuration.GetSection("Logging:CloudWatch");

        if (!cloudWatchConfig.GetValue("Enabled", false)) return loggerConfiguration;

        string logGroupName = cloudWatchConfig.GetValue<string>("LogGroup")
                              ?? $"/aws/application/{applicationName}";

        string region = cloudWatchConfig.GetValue<string>("Region")
                        ?? Environment.GetEnvironmentVariable("AWS_REGION")
                        ?? Environment.GetEnvironmentVariable("AWS_DEFAULT_REGION")
                        ?? "us-east-1";

        var retentionDays = cloudWatchConfig.GetValue("RetentionDays", 30);
        var minimumLevel = cloudWatchConfig.GetValue("MinimumLevel", LogEventLevel.Information);

        var options = new AWSLoggerConfig
        {
            Region = region,
            LogGroup = logGroupName,
            BatchPushInterval
                = TimeSpan.FromSeconds(cloudWatchConfig.GetValue("BatchIntervalSeconds", 5)),
            BatchSizeInBytes = cloudWatchConfig.GetValue("BatchSizeInBytes", 256000),
            MaxQueuedMessages = cloudWatchConfig.GetValue("MaxQueuedMessages", 10000),
            LogStreamNameSuffix = GetLogStreamSuffix()
        };

        // Configure credentials if provided
        var accessKeyId = cloudWatchConfig.GetValue<string>("AccessKeyId");
        var secretAccessKey = cloudWatchConfig.GetValue<string>("SecretAccessKey");

        if (!string.IsNullOrEmpty(accessKeyId) && !string.IsNullOrEmpty(secretAccessKey))
            options.Credentials = new BasicAWSCredentials(accessKeyId, secretAccessKey);

        // Create log group if needed
        var createLogGroup = cloudWatchConfig.GetValue("CreateLogGroup", true);
        if (createLogGroup)
            EnsureLogGroupExists(options, logGroupName, retentionDays).GetAwaiter().GetResult();

        return loggerConfiguration.WriteTo.AWSSeriLog(
            options,
            restrictedToMinimumLevel: minimumLevel);
    }

    /// <summary>
    ///     Configure complete AWS logging with enrichers and CloudWatch
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
        string environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ??
                             "Production";
        string instanceId = EC2InstanceMetadata.InstanceId ?? Environment.MachineName;
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
                client = new AmazonCloudWatchLogsClient(config.Credentials,
                    RegionEndpoint.GetBySystemName(config.Region));
            else
                client = new AmazonCloudWatchLogsClient(
                    RegionEndpoint.GetBySystemName(config.Region));

            using (client)
            {
                // Check if log group exists
                var describeRequest = new DescribeLogGroupsRequest
                {
                    LogGroupNamePrefix = logGroupName
                };

                DescribeLogGroupsResponse? response
                    = await client.DescribeLogGroupsAsync(describeRequest);
                LogGroup? logGroup
                    = response.LogGroups.FirstOrDefault(lg => lg.LogGroupName == logGroupName);

                if (logGroup == null)
                {
                    // Create log group
                    var createRequest = new CreateLogGroupRequest
                    {
                        LogGroupName = logGroupName
                    };
                    await client.CreateLogGroupAsync(createRequest);

                    // Set retention policy
                    var retentionRequest = new PutRetentionPolicyRequest
                    {
                        LogGroupName = logGroupName,
                        RetentionInDays = retentionDays
                    };
                    await client.PutRetentionPolicyAsync(retentionRequest);
                }
                else if (logGroup.RetentionInDays != retentionDays)
                {
                    // Update retention policy
                    var retentionRequest = new PutRetentionPolicyRequest
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