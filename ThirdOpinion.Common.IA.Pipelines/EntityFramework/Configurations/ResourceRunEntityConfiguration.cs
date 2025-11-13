using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThirdOpinion.Common.IA.Pipelines.EntityFramework.Entities;

namespace ThirdOpinion.Common.IA.Pipelines.EntityFramework.Configurations;

internal sealed class ResourceRunEntityConfiguration : IEntityTypeConfiguration<ResourceRunEntity>
{
    public void Configure(EntityTypeBuilder<ResourceRunEntity> builder)
    {
        builder.ToTable("resource_runs");

        builder.HasKey(resource => resource.ResourceRunId);

        builder.Property(resource => resource.Status)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(resource => resource.ResourceId)
            .HasMaxLength(256);

        builder.Property(resource => resource.ResourceType)
            .HasMaxLength(128);

        builder.HasOne(resource => resource.PipelineRun)
            .WithMany(run => run.ResourceRuns)
            .HasForeignKey(resource => resource.PipelineRunId);

        builder.HasIndex(resource => new { resource.PipelineRunId, resource.ResourceId })
            .IsUnique();

        builder.HasIndex(resource => resource.Status);
    }
}


