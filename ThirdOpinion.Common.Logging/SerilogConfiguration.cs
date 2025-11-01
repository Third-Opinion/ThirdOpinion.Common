using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Compact;
using ThirdOpinion.Common.Logging.Aws;
using ILogger = Serilog.ILogger;

namespace ThirdOpinion.Common.Logging;

/// <summary>
///     Configuration helper for Serilog with structured logging support
/// </summary>
public static class SerilogConfiguration
{
    /// <summary>
    ///     Configure Serilog with structured logging capabilities
    /// </summary>
    /// <param name="configuration">Application configuration</param>
    /// <param name="applicationName">Name of the application for context</param>
    /// <returns>Configured Serilog logger</returns>
    public static Logger CreateLogger(IConfiguration configuration, string applicationName)
    {
        LoggerConfiguration logConfig = new LoggerConfiguration()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .Enrich.WithEnvironmentName()
            .Enrich.WithProperty("Application", applicationName);

        // Add correlation ID enricher if available
        logConfig = logConfig.Enrich.With<CorrelationIdEnricher>();

        // Add AWS context enricher
        logConfig = logConfig.Enrich.With<AwsContextEnricher>();

        // Configure console sink with structured output
        var outputTemplate
            = "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}";

        logConfig = logConfig.WriteTo.Console(
            outputTemplate: outputTemplate,
            restrictedToMinimumLevel: LogEventLevel.Debug);

        // Configure file sink with JSON formatting for structured logging
        string logPath = configuration["Logging:FilePath"] ?? "logs/application-.json";
        logConfig = logConfig.WriteTo.File(
            new CompactJsonFormatter(),
            logPath,
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30,
            restrictedToMinimumLevel: LogEventLevel.Information);

        // Read additional configuration from appsettings
        logConfig = logConfig.ReadFrom.Configuration(configuration);

        // Add CloudWatch Logs if configured
        logConfig = logConfig.AddCloudWatchLogs(configuration, applicationName);

        return logConfig.CreateLogger();
    }

    /// <summary>
    ///     Add Serilog to the logging builder
    /// </summary>
    public static ILoggingBuilder AddSerilog(this ILoggingBuilder loggingBuilder, Logger logger)
    {
        loggingBuilder.ClearProviders();
        loggingBuilder.AddSerilog(logger, true);
        return loggingBuilder;
    }

    /// <summary>
    ///     Add Serilog with structured logging to services
    /// </summary>
    public static IServiceCollection AddStructuredLogging(this IServiceCollection services,
        IConfiguration configuration,
        string applicationName)
    {
        Logger logger = CreateLogger(configuration, applicationName);
        Log.Logger = logger;

        services.AddSingleton<ILogger>(logger);
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(logger, true);
        });

        return services;
    }
}

/// <summary>
///     Enricher that adds correlation ID to all log events
/// </summary>
public class CorrelationIdEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        // Try to get correlation ID from AsyncLocal storage (using static helper method)
        string correlationId = CorrelationIdHelper.GetCurrentCorrelationId();

        if (!string.IsNullOrEmpty(correlationId))
            logEvent.AddPropertyIfAbsent(
                propertyFactory.CreateProperty("CorrelationId", correlationId));
    }
}