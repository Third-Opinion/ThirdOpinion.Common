using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Polly;
using Polly.Extensions.Http;

namespace ThirdOpinion.Common.AthenaEhr;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAthenaEhr(this IServiceCollection services, IConfiguration configuration)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));

        // Register configuration
        services.Configure<AthenaConfig>(configuration.GetSection("Athena"));

        // Register HTTP client for OAuth service
        services.AddHttpClient<IAthenaOAuthService, AthenaOAuthService>("AthenaOAuth", (serviceProvider, client) =>
        {
            var config = configuration.GetSection("Athena").Get<AthenaConfig>();
            if (config != null)
            {
                client.BaseAddress = new Uri(config.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(config.RequestTimeoutSeconds);
            }
        });

        // Register HTTP client for FHIR service with Polly retry policies
        services.AddHttpClient<AthenaFhirService>("AthenaFhir", (serviceProvider, client) =>
        {
            var config = configuration.GetSection("Athena").Get<AthenaConfig>();
            if (config != null)
            {
                client.BaseAddress = new Uri(config.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(config.RequestTimeoutSeconds);
            }
        })
        .AddPolicyHandler(GetRetryPolicy())
        .AddPolicyHandler(GetCircuitBreakerPolicy());

        // Register OAuth service
        services.AddScoped<IAthenaOAuthService, AthenaOAuthService>();

        // Register FHIR service
        services.AddScoped<IFhirSourceService, AthenaFhirService>();
        services.AddScoped<AthenaFhirService>();

        return services;
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                3,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    // Log retry attempt
                });
    }

    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                5,
                TimeSpan.FromSeconds(30),
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