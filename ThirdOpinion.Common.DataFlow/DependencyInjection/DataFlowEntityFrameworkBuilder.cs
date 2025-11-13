using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ThirdOpinion.Common.DataFlow.Artifacts;
using ThirdOpinion.Common.DataFlow.EntityFramework;
using ThirdOpinion.Common.DataFlow.Progress;
using ThirdOpinion.Common.DataFlow.Services.Artifacts;
using ThirdOpinion.Common.DataFlow.Services.EfCore;
using ThirdOpinion.Common.DataFlow.Services.Progress;

namespace ThirdOpinion.Common.DataFlow.DependencyInjection;

/// <summary>
/// Builder used to configure Entity Framework Core persistence for the DataFlow library.
/// </summary>
public interface IDataFlowEntityFrameworkBuilder
{
    /// <summary>
    /// Underlying service collection.
    /// </summary>
    IServiceCollection Services { get; }

    /// <summary>
    /// Declare the host application's DbContext that implements <see cref="IDataFlowDbContext"/>.
    /// </summary>
    IDataFlowEntityFrameworkBuilder UseDbContext<TContext>()
        where TContext : class, IDataFlowDbContext;

    /// <summary>
    /// Configure options for the pipeline context pool.
    /// </summary>
    IDataFlowEntityFrameworkBuilder ConfigureContextPool(Action<PipelineContextPoolOptions> configure);

    /// <summary>
    /// Register the default EF Core service implementations (progress tracking, artifact batching, caching).
    /// </summary>
    IDataFlowEntityFrameworkBuilder WithEntityFrameworkServices();
}

internal sealed class DataFlowEntityFrameworkBuilder : IDataFlowEntityFrameworkBuilder
{
    private readonly IServiceCollection _services;
    private bool _servicesRegistered;
    private Type? _dbContextType;

    public DataFlowEntityFrameworkBuilder(IServiceCollection services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _services.AddOptions<PipelineContextPoolOptions>();
    }

    public IServiceCollection Services => _services;

    public IDataFlowEntityFrameworkBuilder UseDbContext<TContext>()
        where TContext : class, IDataFlowDbContext
    {
        _dbContextType = typeof(TContext);
        return this;
    }

    public IDataFlowEntityFrameworkBuilder ConfigureContextPool(Action<PipelineContextPoolOptions> configure)
    {
        if (configure == null) throw new ArgumentNullException(nameof(configure));
        _services.Configure(configure);
        return this;
    }

    public IDataFlowEntityFrameworkBuilder WithEntityFrameworkServices()
    {
        if (_servicesRegistered)
        {
            return this;
        }

        if (_dbContextType is null)
            throw new InvalidOperationException("UseDbContext<TContext>() must be called before registering DataFlow EF services.");

        _servicesRegistered = true;

        _services.TryAddSingleton<PipelineContextPool>();
        _services.TryAddScoped<IDataFlowDbContext>(provider =>
        {
            var context = provider.GetRequiredService(_dbContextType);
            return (IDataFlowDbContext)context;
        });
        _services.TryAddScoped<IPipelineProgressService, EfPipelineProgressService>();
        _services.TryAddScoped<IResourceRunCache, PipelineResourceRunCache>();
        _services.TryAddTransient<PipelineProgressTracker>();
        _services.TryAddScoped<IPipelineProgressTrackerFactory, PipelineProgressTrackerFactory>();
        _services.TryAddScoped<IArtifactStorageService, EfArtifactStorageService>();
        _services.TryAddTransient<PipelineArtifactBatcher>();
        _services.TryAddScoped<IArtifactBatcherFactory, PipelineArtifactBatcherFactory>();

        return this;
    }
}


