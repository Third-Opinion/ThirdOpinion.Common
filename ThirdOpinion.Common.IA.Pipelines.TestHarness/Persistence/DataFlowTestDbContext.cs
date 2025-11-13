using Microsoft.EntityFrameworkCore;
using ThirdOpinion.Common.IA.Pipelines.EntityFramework;
using ThirdOpinion.Common.IA.Pipelines.EntityFramework.Entities;

namespace ThirdOpinion.Common.IA.Pipelines.TestHarness.Persistence;

public class DataFlowTestDbContext(DbContextOptions<DataFlowTestDbContext> options)
    : DbContext(options), IDataFlowDbContext
{
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
