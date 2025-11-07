using Microsoft.Extensions.DependencyInjection;

namespace ThirdOpinion.Common.Misc.Retry;

public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds retry policy services to the dependency injection container
    /// </summary>
    public static IServiceCollection AddRetryPolicies(this IServiceCollection services)
    {
        services.AddSingleton<IRetryPolicyService, RetryPolicyService>();

        return services;
    }
}