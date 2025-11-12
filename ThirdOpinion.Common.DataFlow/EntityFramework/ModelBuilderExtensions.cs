using Microsoft.EntityFrameworkCore;
using ThirdOpinion.Common.DataFlow.EntityFramework.Configurations;

namespace ThirdOpinion.Common.DataFlow.EntityFramework;

/// <summary>
/// Helper extensions for applying DataFlow entity mappings to a host DbContext.
/// </summary>
public static class ModelBuilderExtensions
{
    public static ModelBuilder ApplyDataFlowModel(this ModelBuilder modelBuilder, string? schema = null)
    {
        if (modelBuilder == null)
            throw new ArgumentNullException(nameof(modelBuilder));

        if (!string.IsNullOrWhiteSpace(schema))
        {
            modelBuilder.HasDefaultSchema(schema);
        }

        modelBuilder.ApplyConfiguration(new PipelineRunEntityConfiguration());
        modelBuilder.ApplyConfiguration(new ResourceRunEntityConfiguration());
        modelBuilder.ApplyConfiguration(new StepProgressEntityConfiguration());
        modelBuilder.ApplyConfiguration(new ArtifactEntityConfiguration());

        return modelBuilder;
    }
}


