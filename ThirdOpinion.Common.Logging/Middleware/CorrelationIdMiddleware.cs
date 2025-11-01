using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace ThirdOpinion.Common.Logging.Middleware;

/// <summary>
///     Middleware for managing correlation IDs across HTTP requests
/// </summary>
public class CorrelationIdMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-ID";
    private readonly ILogger<CorrelationIdMiddleware> _logger;
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ICorrelationIdProvider correlationIdProvider)
    {
        string correlationId;

        // Try to get correlation ID from request headers
        if (context.Request.Headers.TryGetValue(CorrelationIdHeader,
                out StringValues correlationIdValues) &&
            !string.IsNullOrWhiteSpace(correlationIdValues.FirstOrDefault()))
        {
            correlationId = correlationIdValues.First()!;
            _logger.LogDebug("Using correlation ID from request header: {CorrelationId}",
                correlationId);
        }
        else
        {
            // Generate new correlation ID
            correlationId = Guid.NewGuid().ToString();
            _logger.LogDebug("Generated new correlation ID: {CorrelationId}", correlationId);
        }

        // Set correlation ID in provider and static helper
        correlationIdProvider.SetCorrelationId(correlationId);
        CorrelationIdHelper.SetCorrelationId(correlationId);

        // Add correlation ID to response headers
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationIdHeader] = correlationId;
            return Task.CompletedTask;
        });

        // Create logging scope with correlation ID
        using (correlationIdProvider.BeginScope(correlationId))
        {
            using (_logger.BeginScope(new Dictionary<string, object>
                       { ["CorrelationId"] = correlationId }))
            {
                await _next(context);
            }
        }
    }
}

/// <summary>
///     Extension methods for adding correlation ID middleware to the pipeline
/// </summary>
public static class CorrelationIdMiddlewareExtensions
{
    /// <summary>
    ///     Add correlation ID middleware to the application pipeline
    /// </summary>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<CorrelationIdMiddleware>();
    }
}