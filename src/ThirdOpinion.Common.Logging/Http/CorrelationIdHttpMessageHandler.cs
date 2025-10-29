using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ThirdOpinion.Common.Logging.Http;

/// <summary>
/// HTTP message handler that adds correlation ID to outgoing requests
/// </summary>
public class CorrelationIdHttpMessageHandler : DelegatingHandler
{
    private readonly ICorrelationIdProvider _correlationIdProvider;
    private readonly ILogger<CorrelationIdHttpMessageHandler> _logger;
    private const string CorrelationIdHeader = "X-Correlation-ID";

    public CorrelationIdHttpMessageHandler(
        ICorrelationIdProvider correlationIdProvider,
        ILogger<CorrelationIdHttpMessageHandler> logger)
    {
        _correlationIdProvider = correlationIdProvider;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var correlationId = _correlationIdProvider.GetCorrelationId();

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            // Add correlation ID to outgoing request headers
            request.Headers.TryAddWithoutValidation(CorrelationIdHeader, correlationId);
            _logger.LogDebug("Added correlation ID {CorrelationId} to outgoing request to {RequestUri}",
                correlationId, request.RequestUri);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}

/// <summary>
/// Extension methods for configuring HTTP clients with correlation ID support
/// </summary>
public static class HttpClientBuilderExtensions
{
    /// <summary>
    /// Add correlation ID propagation to HTTP client
    /// </summary>
    public static IHttpClientBuilder AddCorrelationIdPropagation(this IHttpClientBuilder builder)
    {
        return builder.AddHttpMessageHandler<CorrelationIdHttpMessageHandler>();
    }
}