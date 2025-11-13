using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ThirdOpinion.Common.IA.Pipelines.Artifacts;
using ThirdOpinion.Common.IA.Pipelines.Core;
using ThirdOpinion.Common.IA.Pipelines.Progress;
using ThirdOpinion.Common.IA.Pipelines.Services.S3;
using ThirdOpinion.Common.IA.Pipelines.EntityFramework;
using ThirdOpinion.Common.IA.Pipelines.Services.EfCore;

namespace ThirdOpinion.Common.IA.Pipelines.DependencyInjection;

/// <summary>
/// Extension methods for registering DataFlow services with dependency injection
/// </summary>
public static class DataFlowServiceCollectionExtensions
{
    /// <summary>
    /// Adds core ThirdOpinion DataFlow services to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>A builder for configuring DataFlow services</returns>
    public static IDataFlowBuilder AddThirdOpinionDataFlow(this IServiceCollection services)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        // Register the pipeline context factory as scoped (one per pipeline run typically)
        services.TryAddScoped<IPipelineContextFactory, PipelineContextFactory>();

        return new DataFlowBuilder(services);
    }

    /// <summary>
    /// Adds Entity Framework Core persistence for DataFlow and returns a builder for configuration.
    /// </summary>
    public static IDataFlowEntityFrameworkBuilder AddEntityFrameworkStorage(this IDataFlowBuilder builder)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));

        return new DataFlowEntityFrameworkBuilder(builder.Services);
    }

    /// <summary>
    /// Registers a custom IPipelineProgressService implementation
    /// </summary>
    /// <typeparam name="TService">The service implementation type</typeparam>
    /// <param name="builder">The DataFlow builder</param>
    /// <param name="lifetime">Service lifetime (default: Scoped)</param>
    /// <returns>The builder for chaining</returns>
    public static IDataFlowBuilder WithProgressService<TService>(
        this IDataFlowBuilder builder,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TService : class, IPipelineProgressService
    {
        builder.Services.Add(new ServiceDescriptor(
            typeof(IPipelineProgressService),
            typeof(TService),
            lifetime));

        return builder;
    }

    /// <summary>
    /// Registers a custom IPipelineProgressTrackerFactory implementation
    /// </summary>
    /// <typeparam name="TFactory">The factory implementation type</typeparam>
    /// <param name="builder">The DataFlow builder</param>
    /// <param name="lifetime">Service lifetime (default: Scoped)</param>
    /// <returns>The builder for chaining</returns>
    public static IDataFlowBuilder WithProgressTrackerFactory<TFactory>(
        this IDataFlowBuilder builder,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TFactory : class, IPipelineProgressTrackerFactory
    {
        builder.Services.Add(new ServiceDescriptor(
            typeof(IPipelineProgressTrackerFactory),
            typeof(TFactory),
            lifetime));

        return builder;
    }

    /// <summary>
    /// Registers a custom IArtifactBatcherFactory implementation
    /// </summary>
    /// <typeparam name="TFactory">The factory implementation type</typeparam>
    /// <param name="builder">The DataFlow builder</param>
    /// <param name="lifetime">Service lifetime (default: Scoped)</param>
    /// <returns>The builder for chaining</returns>
    public static IDataFlowBuilder WithArtifactBatcherFactory<TFactory>(
        this IDataFlowBuilder builder,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TFactory : class, IArtifactBatcherFactory
    {
        builder.Services.Add(new ServiceDescriptor(
            typeof(IArtifactBatcherFactory),
            typeof(TFactory),
            lifetime));

        return builder;
    }

    /// <summary>
    /// Registers a custom IArtifactStorageService implementation
    /// </summary>
    /// <typeparam name="TStorage">The storage implementation type</typeparam>
    /// <param name="builder">The DataFlow builder</param>
    /// <param name="lifetime">Service lifetime (default: Scoped)</param>
    /// <returns>The builder for chaining</returns>
    public static IDataFlowBuilder WithArtifactStorage<TStorage>(
        this IDataFlowBuilder builder,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TStorage : class, IArtifactStorageService
    {
        builder.Services.Add(new ServiceDescriptor(
            typeof(IArtifactStorageService),
            typeof(TStorage),
            lifetime));

        return builder;
    }

    /// <summary>
    /// Registers the Amazon S3 artifact storage implementation and optional configuration.
    /// </summary>
    /// <param name="builder">The DataFlow builder.</param>
    /// <param name="configureOptions">Optional delegate used to configure <see cref="S3ArtifactStorageOptions"/>.</param>
    /// <param name="lifetime">Service lifetime (default: Scoped).</param>
    /// <returns>The builder for chaining.</returns>
    public static IDataFlowBuilder WithS3ArtifactStorage(
        this IDataFlowBuilder builder,
        Action<S3ArtifactStorageOptions>? configureOptions = null,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));

        builder.Services.AddOptions<S3ArtifactStorageOptions>();
        if (configureOptions != null)
        {
            builder.Services.Configure(configureOptions);
        }

        builder.Services.Add(new ServiceDescriptor(
            typeof(Services.S3.S3ArtifactStorageService),
            typeof(Services.S3.S3ArtifactStorageService),
            lifetime));

        builder.Services.Add(new ServiceDescriptor(
            typeof(IArtifactStorageService),
            sp => sp.GetRequiredService<Services.S3.S3ArtifactStorageService>(),
            lifetime));

        return builder;
    }

    /// <summary>
    /// Registers a custom IResourceRunCache implementation
    /// </summary>
    /// <typeparam name="TCache">The cache implementation type</typeparam>
    /// <param name="builder">The DataFlow builder</param>
    /// <param name="lifetime">Service lifetime (default: Scoped)</param>
    /// <returns>The builder for chaining</returns>
    public static IDataFlowBuilder WithResourceRunCache<TCache>(
        this IDataFlowBuilder builder,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TCache : class, IResourceRunCache
    {
        builder.Services.Add(new ServiceDescriptor(
            typeof(IResourceRunCache),
            typeof(TCache),
            lifetime));

        return builder;
    }

    /// <summary>
    /// Uses in-memory implementations for all DataFlow services (useful for testing)
    /// </summary>
    /// <param name="builder">The DataFlow builder</param>
    /// <returns>The builder for chaining</returns>
    public static IDataFlowBuilder UseInMemoryServices(this IDataFlowBuilder builder)
    {
        // Register in-memory implementations for testing/development
        // Note: IPipelineProgressService must be provided by the application
        builder.Services.TryAddScoped<IPipelineProgressTrackerFactory, Services.InMemory.InMemoryProgressTrackerFactory>();
        builder.Services.TryAddScoped<IArtifactStorageService, Services.InMemory.InMemoryArtifactStorageService>();
        builder.Services.TryAddScoped<IArtifactBatcherFactory, Services.InMemory.InMemoryArtifactBatcherFactory>();
        builder.Services.TryAddScoped<IResourceRunCache, Services.InMemory.InMemoryResourceRunCache>();
        
        return builder;
    }
}

/// <summary>
/// Builder interface for configuring DataFlow services
/// </summary>
public interface IDataFlowBuilder
{
    /// <summary>
    /// Gets the service collection being configured
    /// </summary>
    IServiceCollection Services { get; }
}

/// <summary>
/// Default implementation of IDataFlowBuilder
/// </summary>
internal class DataFlowBuilder : IDataFlowBuilder
{
    public DataFlowBuilder(IServiceCollection services)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
    }

    public IServiceCollection Services { get; }
}

