using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ThirdOpinion.Common.Logging;

public static class LoggingExtensions
{
    public static IServiceCollection AddFhirToolsLogging(this IServiceCollection services,
        LogLevel logLevel = LogLevel.Information)
    {
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddConsole();
            builder.SetMinimumLevel(logLevel);

            // Add custom log filters
            builder.AddFilter<SensitiveDataFilter>("FhirTools", LogLevel.Trace);
        });

        services.AddSingleton<ILoggerProvider, SensitiveDataLoggerProvider>();
        services.AddScoped<ICorrelationIdProvider, CorrelationIdProvider>();

        return services;
    }

    public static LogLevel MapVerbosityToLogLevel(string verbosity)
    {
        return verbosity.ToLowerInvariant() switch
        {
            "verbose" => LogLevel.Trace,
            "debug" => LogLevel.Debug,
            "information" => LogLevel.Information,
            "warning" => LogLevel.Warning,
            "error" => LogLevel.Error,
            _ => LogLevel.Information
        };
    }
}