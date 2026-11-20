using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prodexa.Core.Materials;

namespace Prodexa.Persistence.Configurations;

/// <summary>
/// Maps material lots to their material definitions and source creation times.
/// </summary>
public sealed class MaterialLotConfiguration : IEntityTypeConfiguration<MaterialLot>
{
    public void Configure(EntityTypeBuilder<MaterialLot> builder)
    {
        builder.ToTable("material_lots", table =>
        {
            table.HasCheckConstraint("ck_material_lots_initial_quantity", "\"InitialQuantity\" IS NULL OR \"InitialQuantity\" >= 0");
        });

        builder.HasKey(lot => lot.Id);
        builder.Property(lot => lot.Id).HasMaxLength(100).ValueGeneratedNever();
        builder.Property(lot => lot.MaterialId).HasMaxLength(100);
        builder.Property(lot => lot.UnitId).HasMaxLength(50);
        builder.Property(lot => lot.Status).HasMaxLength(32);
        builder.Property(lot => lot.InitialQuantity).HasPrecision(18, 6);

        builder.HasOne<MaterialDefinition>().WithMany().HasForeignKey(lot => lot.MaterialId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(lot => lot.MaterialId);
    }
}
