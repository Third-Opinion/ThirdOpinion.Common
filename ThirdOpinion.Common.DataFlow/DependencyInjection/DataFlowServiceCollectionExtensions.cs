using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ThirdOpinion.Common.DataFlow.Artifacts;
using ThirdOpinion.Common.DataFlow.Core;
using ThirdOpinion.Common.DataFlow.Progress;
using ThirdOpinion.Common.DataFlow.Services.InMemory;
using ThirdOpinion.Common.DataFlow.Services.S3;

namespace ThirdOpinion.Common.DataFlow.DependencyInjection;

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
        // Use a factory delegate to automatically inject IPipelineProgressService if available
        services.TryAddScoped<IPipelineContextFactory>(sp =>
        {
            var logger = sp.GetService<Microsoft.Extensions.Logging.ILogger<PipelineContextFactory>>();
            var progressTrackerFactory = sp.GetService<IPipelineProgressTrackerFactory>();
            var artifactBatcherFactory = sp.GetService<IArtifactBatcherFactory>();
            var resourceRunCache = sp.GetService<IResourceRunCache>();
            var progressService = sp.GetService<IPipelineProgressService>();
            
            return new PipelineContextFactory(
                logger,
                progressTrackerFactory,
                artifactBatcherFactory,
                resourceRunCache,
                progressService);
        });

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
            typeof(S3ArtifactStorageService),
            typeof(S3ArtifactStorageService),
            lifetime));

        builder.Services.Add(new ServiceDescriptor(
            typeof(IArtifactStorageService),
            sp => sp.GetRequiredService<S3ArtifactStorageService>(),
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
        builder.Services.TryAddScoped<IPipelineProgressTrackerFactory, InMemoryProgressTrackerFactory>();
        builder.Services.TryAddScoped<IArtifactStorageService, InMemoryArtifactStorageService>();
        builder.Services.TryAddScoped<IArtifactBatcherFactory, InMemoryArtifactBatcherFactory>();
        builder.Services.TryAddScoped<IResourceRunCache, InMemoryResourceRunCache>();
        
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

