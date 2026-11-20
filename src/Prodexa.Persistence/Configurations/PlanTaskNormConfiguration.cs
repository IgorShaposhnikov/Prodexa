using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prodexa.Core.Materials;
using Prodexa.Core.Production;

namespace Prodexa.Persistence.Configurations;

/// <summary>
/// Binds one material-specific norm version to a particular production task revision.
/// </summary>
public sealed class PlanTaskNormConfiguration : IEntityTypeConfiguration<PlanTaskNorm>
{
    public void Configure(EntityTypeBuilder<PlanTaskNorm> builder)
    {
        builder.ToTable("plan_task_norms");

        builder.HasKey(binding => new { binding.TaskRevisionId, binding.MaterialId });
        builder.Property(binding => binding.TaskRevisionId).HasMaxLength(100);
        builder.Property(binding => binding.MaterialId).HasMaxLength(100);
        builder.Property(binding => binding.NormVersionId).HasMaxLength(100);

        builder.HasOne<PlanTaskRevision>().WithMany().HasForeignKey(binding => binding.TaskRevisionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<MaterialDefinition>().WithMany().HasForeignKey(binding => binding.MaterialId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ConsumptionNorm>().WithMany().HasForeignKey(binding => binding.NormVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(binding => binding.NormVersionId);
    }
}
