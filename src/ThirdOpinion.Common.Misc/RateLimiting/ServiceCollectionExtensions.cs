using Microsoft.Extensions.DependencyInjection;

namespace ThirdOpinion.Common.Misc.RateLimiting;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds rate limiting services to the dependency injection container
    /// </summary>
    public static IServiceCollection AddRateLimiting(this IServiceCollection services)
    {
        services.AddSingleton<IRateLimiterService, GenericRateLimiterService>();

        return services;
    }
}