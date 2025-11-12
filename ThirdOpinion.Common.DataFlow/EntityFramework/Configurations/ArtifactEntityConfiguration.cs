using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThirdOpinion.Common.DataFlow.EntityFramework.Entities;

namespace ThirdOpinion.Common.DataFlow.EntityFramework.Configurations;

internal sealed class ArtifactEntityConfiguration : IEntityTypeConfiguration<ArtifactEntity>
{
    public void Configure(EntityTypeBuilder<ArtifactEntity> builder)
    {
        builder.ToTable("artifacts");

        builder.HasKey(artifact => artifact.ArtifactId);

        builder.Property(artifact => artifact.StorageType)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(artifact => artifact.StepName)
            .HasMaxLength(256);

        builder.Property(artifact => artifact.ArtifactName)
            .HasMaxLength(256);

        builder.Property(artifact => artifact.DataJson)
            .HasColumnType("jsonb");

        builder.Property(artifact => artifact.MetadataJson)
            .HasColumnType("jsonb");

        builder.Property(artifact => artifact.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(artifact => new { artifact.ResourceRunId, artifact.StepName, artifact.ArtifactName })
            .IsUnique();

        builder.HasOne(artifact => artifact.ResourceRun)
            .WithMany(resource => resource.Artifacts)
            .HasForeignKey(artifact => artifact.ResourceRunId);
    }
}


