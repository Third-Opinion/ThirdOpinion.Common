using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThirdOpinion.Common.DataFlow.EntityFramework.Entities;

namespace ThirdOpinion.Common.DataFlow.EntityFramework.Configurations;

internal sealed class PipelineRunEntityConfiguration : IEntityTypeConfiguration<PipelineRunEntity>
{
    public void Configure(EntityTypeBuilder<PipelineRunEntity> builder)
    {
        builder.ToTable("pipeline_runs");

        builder.HasKey(run => run.RunId);

        builder.Property(run => run.RunType)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(run => run.Status)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(run => run.Category)
            .HasMaxLength(256);

        builder.Property(run => run.Name)
            .HasMaxLength(256);

        builder.Property(run => run.Configuration)
            .HasColumnType("jsonb");

        builder.HasIndex(run => new { run.Category, run.Name });

        builder.HasMany(run => run.ChildRuns)
            .WithOne(run => run.ParentRun)
            .HasForeignKey(run => run.ParentRunId);
    }
}


