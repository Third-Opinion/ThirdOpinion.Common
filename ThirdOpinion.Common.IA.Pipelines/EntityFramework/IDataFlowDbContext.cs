using Microsoft.EntityFrameworkCore;
using ThirdOpinion.Common.IA.Pipelines.EntityFramework.Entities;

namespace ThirdOpinion.Common.IA.Pipelines.EntityFramework;

/// <summary>
/// Contract that host applications implement to provide persistence for DataFlow pipelines.
/// </summary>
public interface IDataFlowDbContext : IDisposable
{
    DbSet<PipelineRunEntity> PipelineRuns { get; }
    DbSet<ResourceRunEntity> ResourceRuns { get; }
    DbSet<StepProgressEntity> StepProgresses { get; }
    DbSet<ArtifactEntity> Artifacts { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}


