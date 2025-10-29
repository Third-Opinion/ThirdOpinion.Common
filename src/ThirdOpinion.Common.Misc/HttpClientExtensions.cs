using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
using System.Net;
using ThirdOpinion.Common.Misc.RateLimiting;
using ThirdOpinion.Common.Misc.Retry;

namespace ThirdOpinion.Common.Misc;

/// <summary>
/// Extension methods for configuring HttpClient with retry and rate limiting policies
/// </summary>
public static class HttpClientExtensions
{
    /// <summary>
    /// Adds an HttpClient with retry policy
    /// </summary>
    public static IHttpClientBuilder AddHttpClientWithRetry(
        this IServiceCollection services,
        string name,
        Action<HttpClient>? configureClient = null,
        int retryCount = 3,
        int baseDelaySeconds = 2)
    {
        return services.AddHttpClient(name, configureClient ?? (_ => { }))
            .AddPolicyHandler(GetRetryPolicy(retryCount, baseDelaySeconds));
    }

    /// <summary>
    /// Adds an HttpClient with rate limiting
    /// </summary>
    public static IHttpClientBuilder AddHttpClientWithRateLimit(
        this IServiceCollection services,
        string name,
        Action<HttpClient>? configureClient = null,
        int requestsPerMinute = 60)
    {
        var builder = services.AddHttpClient(name, configureClient ?? (_ => { }));

        // Add rate limiting handler
        builder.AddHttpMessageHandler(serviceProvider =>
        {
            var rateLimiterService = serviceProvider.GetRequiredService<IRateLimiterService>();
            // Register the rate limiter for this service if not already registered
            if (rateLimiterService is GenericRateLimiterService genericService)
            {
                genericService.RegisterRateLimiter(name, requestsPerMinute);
            }
            var rateLimiter = rateLimiterService.GetRateLimiter(name);
            var logger = serviceProvider.GetService<ILogger<RateLimitingHttpMessageHandler>>();
            return new RateLimitingHttpMessageHandler(rateLimiter, logger);
        });

        return builder;
    }

    /// <summary>
    /// Adds an HttpClient with both retry policy and rate limiting
    /// </summary>
    public static IHttpClientBuilder AddHttpClientWithRetryAndRateLimit(
        this IServiceCollection services,
        string name,
        Action<HttpClient>? configureClient = null,
        int retryCount = 3,
        int baseDelaySeconds = 2,
        int requestsPerMinute = 60)
    {
        var builder = services.AddHttpClient(name, configureClient ?? (_ => { }))
            .AddPolicyHandler(GetRetryPolicy(retryCount, baseDelaySeconds));

        // Add rate limiting handler
        builder.AddHttpMessageHandler(serviceProvider =>
        {
            var rateLimiterService = serviceProvider.GetRequiredService<IRateLimiterService>();
            // Register the rate limiter for this service if not already registered
            if (rateLimiterService is GenericRateLimiterService genericService)
            {
                genericService.RegisterRateLimiter(name, requestsPerMinute);
            }
            var rateLimiter = rateLimiterService.GetRateLimiter(name);
            var logger = serviceProvider.GetService<ILogger<RateLimitingHttpMessageHandler>>();
            return new RateLimitingHttpMessageHandler(rateLimiter, logger);
        });

        return builder;
    }

    /// <summary>
    /// Adds a retry policy to an existing HttpClientBuilder
    /// </summary>
    public static IHttpClientBuilder AddRetryPolicy(
        this IHttpClientBuilder builder,
        int retryCount = 3,
        int baseDelaySeconds = 2)
    {
        return builder.AddPolicyHandler(GetRetryPolicy(retryCount, baseDelaySeconds));
    }

    /// <summary>
    /// Adds rate limiting to an existing HttpClientBuilder
    /// </summary>
    public static IHttpClientBuilder AddRateLimiting(
        this IHttpClientBuilder builder,
        string serviceName,
        int requestsPerMinute = 60)
    {
        return builder.AddHttpMessageHandler(serviceProvider =>
        {
            var rateLimiterService = serviceProvider.GetRequiredService<IRateLimiterService>();
            // Register the rate limiter for this service if not already registered
            if (rateLimiterService is GenericRateLimiterService genericService)
            {
                genericService.RegisterRateLimiter(serviceName, requestsPerMinute);
            }
            var rateLimiter = rateLimiterService.GetRateLimiter(serviceName);
            var logger = serviceProvider.GetService<ILogger<RateLimitingHttpMessageHandler>>();
            return new RateLimitingHttpMessageHandler(rateLimiter, logger);
        });
    }

    /// <summary>
    /// Adds a circuit breaker policy to an existing HttpClientBuilder
    /// </summary>
    public static IHttpClientBuilder AddCircuitBreaker(
        this IHttpClientBuilder builder,
        int handledEventsAllowedBeforeBreaking = 5,
        int durationOfBreakSeconds = 30)
    {
        return builder.AddPolicyHandler(GetCircuitBreakerPolicy(
            handledEventsAllowedBeforeBreaking,
            durationOfBreakSeconds));
    }

    /// <summary>
    /// Adds combined retry and circuit breaker policies
    /// </summary>
    public static IHttpClientBuilder AddResilience(
        this IHttpClientBuilder builder,
        int retryCount = 3,
        int baseDelaySeconds = 2,
        int handledEventsAllowedBeforeBreaking = 5,
        int durationOfBreakSeconds = 30)
    {
        var retryPolicy = GetRetryPolicy(retryCount, baseDelaySeconds);
        var circuitBreakerPolicy = GetCircuitBreakerPolicy(
            handledEventsAllowedBeforeBreaking,
            durationOfBreakSeconds);

        // Wrap retry around circuit breaker
        var combinedPolicy = Policy.WrapAsync(retryPolicy, circuitBreakerPolicy);

        return builder.AddPolicyHandler(combinedPolicy);
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(int retryCount, int baseDelaySeconds)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError() // Handles HttpRequestException and 5XX, 408 status codes
            .OrResult(msg => msg.StatusCode == HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                retryCount,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(baseDelaySeconds, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    // Logging handled by Polly internally
                });
    }

    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy(
        int handledEventsAllowedBeforeBreaking,
        int durationOfBreakSeconds)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => msg.StatusCode == HttpStatusCode.TooManyRequests)
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking,
                TimeSpan.FromSeconds(durationOfBreakSeconds),
                onBreak: (result, duration) =>
                {
                    // Circuit breaker opened
                },
                onReset: () =>
                {
                    // Circuit breaker reset
                });
    }
}