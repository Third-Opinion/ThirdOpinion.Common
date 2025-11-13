using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ThirdOpinion.Common.DataFlow.Artifacts;
using ThirdOpinion.Common.DataFlow.DependencyInjection;
using ThirdOpinion.Common.DataFlow.EntityFramework;
using ThirdOpinion.Common.DataFlow.EntityFramework.Entities;
using ThirdOpinion.Common.DataFlow.Models;
using ThirdOpinion.Common.DataFlow.Progress;
using ThirdOpinion.Common.DataFlow.Progress.Models;
using ThirdOpinion.Common.DataFlow.Services.EfCore;

namespace ThirdOpinion.Common.DataFlow.Tests.EntityFramework;

public class EfPipelineProgressServiceTests
{
    [Fact]
    public async Task CreateRun_PersistsResourcesAndSteps()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var databaseRoot = new InMemoryDatabaseRoot();
        services.AddDbContext<TestHostDataFlowDbContext>(options => options.UseInMemoryDatabase("dataflow-tests", databaseRoot));
        services.AddScoped<IDataFlowDbContext>(sp => sp.GetRequiredService<TestHostDataFlowDbContext>());
        services.Configure<PipelineContextPoolOptions>(options => options.MaxConcurrentContexts = 4);

        using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var poolOptions = provider.GetRequiredService<IOptions<PipelineContextPoolOptions>>();

        var contextPool = new PipelineContextPool(scopeFactory, poolOptions, NullLogger<PipelineContextPool>.Instance);
        var progressService = new EfPipelineProgressService(contextPool, NullLogger<EfPipelineProgressService>.Instance);

        var run = await progressService.CreateRunAsync(new CreatePipelineRunRequest
        {
            Category = "Tests",
            Name = "EfPipeline",
            RunType = PipelineRunType.Fresh
        }, CancellationToken.None);

        var resourceUpdates = new[]
        {
            new ResourceProgressUpdate
            {
                ResourceRunId = Guid.NewGuid(),
                ResourceId = "patient-1",
                ResourceType = "Patient",
                Status = PipelineResourceStatus.Processing,
                StartTime = DateTime.UtcNow
            },
            new ResourceProgressUpdate
            {
                ResourceRunId = Guid.NewGuid(),
                ResourceId = "patient-2",
                ResourceType = "Patient",
                Status = PipelineResourceStatus.Processing,
                StartTime = DateTime.UtcNow
            }
        };

        await progressService.CreateResourceRunsBatchAsync(run.Id, resourceUpdates, CancellationToken.None);

        var stepUpdates = resourceUpdates.Select(update => new StepProgressUpdate
        {
            ResourceRunId = update.ResourceRunId,
            StepName = "Normalize",
            Status = PipelineStepStatus.Completed,
            DurationMs = 120,
            EndTime = DateTime.UtcNow
        }).ToArray();

        var deferredSteps = await progressService.UpdateStepProgressBatchAsync(run.Id, stepUpdates, CancellationToken.None);
        Assert.Empty(deferredSteps);

        var completionUpdates = resourceUpdates.Select(update => new ResourceCompletionUpdate
        {
            ResourceRunId = update.ResourceRunId,
            FinalStatus = PipelineResourceStatus.Completed,
            EndTime = DateTime.UtcNow
        }).ToArray();

        await progressService.CompleteResourceRunsBatchAsync(run.Id, completionUpdates, CancellationToken.None);
        await progressService.CompleteRunAsync(run.Id, PipelineRunStatus.Completed, CancellationToken.None);

        using var verificationScope = provider.CreateScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<TestHostDataFlowDbContext>();
        var persistedRun = await verificationContext.PipelineRuns
            .Include(r => r.ResourceRuns)
            .SingleOrDefaultAsync(r => r.RunId == run.Id);
        Assert.NotNull(persistedRun);
        Assert.Equal(PipelineRunStatus.Completed, persistedRun!.Status);
        Assert.Equal(2, persistedRun.ResourceRuns.Count);
        Assert.Equal(2, persistedRun.CompletedResources);

        var stepCount = await verificationContext.StepProgresses.CountAsync();
        Assert.Equal(2, stepCount);

        contextPool.Dispose();
    }

    [Fact]
    public void EntityFrameworkBuilder_RegistersDefaultServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<TestHostDataFlowDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        services.AddThirdOpinionDataFlow()
            .AddEntityFrameworkStorage()
            .UseDbContext<TestHostDataFlowDbContext>()
            .ConfigureContextPool(options => options.MaxConcurrentContexts = 4)
            .WithEntityFrameworkServices();

        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<PipelineContextPool>());
        Assert.NotNull(provider.GetService<IPipelineProgressService>());
        Assert.NotNull(provider.GetService<IResourceRunCache>());
        Assert.NotNull(provider.GetService<IArtifactBatcherFactory>());
        Assert.NotNull(provider.GetService<IPipelineProgressTrackerFactory>());
    }

    private sealed class TestHostDataFlowDbContext : DbContext, IDataFlowDbContext
    {
        public TestHostDataFlowDbContext(DbContextOptions<TestHostDataFlowDbContext> options)
            : base(options)
        {
        }

        public DbSet<PipelineRunEntity> PipelineRuns => Set<PipelineRunEntity>();
        public DbSet<ResourceRunEntity> ResourceRuns => Set<ResourceRunEntity>();
        public DbSet<StepProgressEntity> StepProgresses => Set<StepProgressEntity>();
        public DbSet<ArtifactEntity> Artifacts => Set<ArtifactEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyDataFlowModel();
            base.OnModelCreating(modelBuilder);
        }
    }
}

