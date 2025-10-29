using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace ThirdOpinion.Common.Logging;

public class SensitiveDataFilter : ILoggerProvider
{
    private readonly ILoggerProvider _innerProvider;
    
    public SensitiveDataFilter(ILoggerProvider innerProvider)
    {
        _innerProvider = innerProvider;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new SensitiveDataLogger(_innerProvider.CreateLogger(categoryName));
    }

    public void Dispose()
    {
        _innerProvider.Dispose();
    }
}

public class SensitiveDataLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName)
    {
        return new SensitiveDataLogger(new ConsoleLogger(categoryName));
    }

    public void Dispose()
    {
        // No resources to dispose
    }
}

public class SensitiveDataLogger : ILogger
{
    private readonly ILogger _innerLogger;
    
    // Patterns for sensitive data
    private static readonly Regex[] SensitivePatterns = {
        new(@"(?i)(access_token|bearer)\s*[=:]\s*[""']?([a-zA-Z0-9_\-\.]+)[""']?", RegexOptions.Compiled),
        new(@"(?i)(client_secret|secret)\s*[=:]\s*[""']?([a-zA-Z0-9_\-\.]+)[""']?", RegexOptions.Compiled),
        new(@"(?i)(password|pwd)\s*[=:]\s*[""']?([^\s""']+)[""']?", RegexOptions.Compiled),
        new(@"(?i)(authorization:\s*bearer\s+)([a-zA-Z0-9_\-\.]+)", RegexOptions.Compiled),
        // PHI patterns - basic examples (would need more comprehensive patterns in production)
        new(@"\b\d{3}-\d{2}-\d{4}\b", RegexOptions.Compiled), // SSN pattern
        new(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", RegexOptions.Compiled), // Email pattern
    };

    public SensitiveDataLogger(ILogger innerLogger)
    {
        _innerLogger = innerLogger;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return _innerLogger.BeginScope(state);
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return _innerLogger.IsEnabled(logLevel);
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var message = formatter(state, exception);
        var sanitizedMessage = SanitizeMessage(message);
        
        _innerLogger.Log(logLevel, eventId, sanitizedMessage, exception, (msg, ex) => msg);
    }

    private static string SanitizeMessage(string message)
    {
        if (string.IsNullOrEmpty(message))
            return message;

        var sanitized = message;
        
        foreach (var pattern in SensitivePatterns)
        {
            sanitized = pattern.Replace(sanitized, match =>
            {
                var groups = match.Groups;
                if (groups.Count >= 3)
                {
                    var prefix = groups[1].Value;
                    var sensitiveValue = groups[2].Value;
                    var maskedValue = MaskSensitiveValue(sensitiveValue);
                    return $"{prefix}={maskedValue}";
                }
                return "[REDACTED]";
            });
        }

        return sanitized;
    }

    private static string MaskSensitiveValue(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        if (value.Length <= 4)
            return new string('*', value.Length);

        return value.Substring(0, 2) + new string('*', value.Length - 4) + value.Substring(value.Length - 2);
    }
}

public class ConsoleLogger : ILogger
{
    private readonly string _categoryName;

    public ConsoleLogger(string categoryName)
    {
        _categoryName = categoryName;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{logLevel}] {_categoryName}: {message}");
        
        if (exception != null)
        {
            Console.WriteLine($"Exception: {exception}");
        }
    }
}