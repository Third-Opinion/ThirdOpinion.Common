using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ThirdOpinion.Common.Misc.RateLimiting;

/// <summary>
///     HTTP message handler that applies rate limiting and adapts based on responses
/// </summary>
public class RateLimitingHttpMessageHandler : DelegatingHandler
{
    private readonly AdaptiveRateLimiter? _adaptiveRateLimiter;
    private readonly ILogger<RateLimitingHttpMessageHandler>? _logger;
    private readonly IRateLimiter _rateLimiter;

    public RateLimitingHttpMessageHandler(
        IRateLimiter rateLimiter,
        ILogger<RateLimitingHttpMessageHandler>? logger = null)
    {
        _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
        _adaptiveRateLimiter = rateLimiter as AdaptiveRateLimiter;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Apply rate limiting before sending request
        await _rateLimiter.WaitAsync(cancellationToken);

        _logger?.LogDebug(
            "Sending rate-limited request to {Uri} for service {ServiceName}",
            request.RequestUri, _rateLimiter.ServiceName);

        try
        {
            HttpResponseMessage response = await base.SendAsync(request, cancellationToken);

            // Notify adaptive rate limiter of response
            if (_adaptiveRateLimiter != null)
            {
                var retryAfter = response.Headers.RetryAfter?.ToString();
                _adaptiveRateLimiter.OnHttpResponse(response.StatusCode, retryAfter);

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    _logger?.LogWarning(
                        "Received HTTP 429 from {Uri}. Retry-After: {RetryAfter}",
                        request.RequestUri, retryAfter ?? "not specified");
            }

            return response;
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex,
                "HTTP request failed for {Uri} in service {ServiceName}",
                request.RequestUri, _rateLimiter.ServiceName);
            throw;
        }
    }
}

/// <summary>
///     Extension methods for configuring HTTP clients with rate limiting
/// </summary>
public static class HttpClientBuilderExtensions
{
    /// <summary>
    ///     Adds rate limiting to an HTTP client
    /// </summary>
    public static IHttpClientBuilder AddRateLimiting(
        this IHttpClientBuilder builder,
        string serviceName,
        bool adaptive = true)
    {
        builder.Services.AddTransient<RateLimitingHttpMessageHandler>(provider =>
        {
            var rateLimiterService = provider.GetRequiredService<IRateLimiterService>();
            IRateLimiter rateLimiter = rateLimiterService.GetRateLimiter(serviceName);

            if (adaptive)
            {
                var adaptiveLogger = provider.GetService<ILogger<AdaptiveRateLimiter>>();
                rateLimiter = rateLimiter.WithAdaptiveBehavior(adaptiveLogger);
            }

            var handlerLogger = provider.GetService<ILogger<RateLimitingHttpMessageHandler>>();
            return new RateLimitingHttpMessageHandler(rateLimiter, handlerLogger);
        });

        builder.AddHttpMessageHandler<RateLimitingHttpMessageHandler>();

        return builder;
    }
}