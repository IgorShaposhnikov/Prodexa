using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prodexa.Core.Materials;

namespace Prodexa.Persistence.Configurations;

/// <summary>
/// Maps immutable material consumption norm versions and their product and input material versions.
/// </summary>
public sealed class ConsumptionNormConfiguration : IEntityTypeConfiguration<ConsumptionNorm>
{
    public void Configure(EntityTypeBuilder<ConsumptionNorm> builder)
    {
        builder.ToTable("consumption_norms", table =>
        {
            table.HasCheckConstraint("ck_consumption_norms_quantity", "\"QuantityPerOutput\" > 0");
            table.HasCheckConstraint("ck_consumption_norms_revision", "\"VersionNumber\" > 0");
            table.HasCheckConstraint("ck_consumption_norms_period", "\"ValidTo\" IS NULL OR \"ValidTo\" > \"ValidFrom\"");
        });

        builder.HasKey(norm => norm.Id);
        builder.Property(norm => norm.Id).HasMaxLength(100).ValueGeneratedNever();
        builder.Property(norm => norm.NormId).HasMaxLength(100);
        builder.Property(norm => norm.ProductVersionId).HasMaxLength(100);
        builder.Property(norm => norm.MaterialVersionId).HasMaxLength(100);
        builder.Property(norm => norm.ProcessSegmentVersionId).HasMaxLength(100);
        builder.Property(norm => norm.InputUnitId).HasMaxLength(50);
        builder.Property(norm => norm.OutputUnitId).HasMaxLength(50);
        builder.Property(norm => norm.QuantityPerOutput).HasPrecision(18, 6);

        builder.HasOne<MaterialDefinitionVersion>().WithMany().HasForeignKey(norm => norm.ProductVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<MaterialDefinitionVersion>().WithMany().HasForeignKey(norm => norm.MaterialVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(norm => new { norm.NormId, norm.VersionNumber }).IsUnique();
    }
}
