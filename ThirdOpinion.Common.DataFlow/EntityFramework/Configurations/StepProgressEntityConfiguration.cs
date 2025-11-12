using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThirdOpinion.Common.DataFlow.EntityFramework.Entities;

namespace ThirdOpinion.Common.DataFlow.EntityFramework.Configurations;

internal sealed class StepProgressEntityConfiguration : IEntityTypeConfiguration<StepProgressEntity>
{
    public void Configure(EntityTypeBuilder<StepProgressEntity> builder)
    {
        builder.ToTable("step_progress");

        builder.HasKey(step => step.StepProgressId);

        builder.Property(step => step.Status)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(step => step.StepName)
            .HasMaxLength(256);

        builder.Property(step => step.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(step => new { step.ResourceRunId, step.Sequence });

        builder.HasOne(step => step.ResourceRun)
            .WithMany(resource => resource.StepProgresses)
            .HasForeignKey(step => step.ResourceRunId);
    }
}


