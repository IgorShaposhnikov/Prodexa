using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prodexa.Core.Materials;
using Prodexa.Core.Production;

namespace Prodexa.Persistence.Configurations;

/// <summary>
/// Maps immutable material events to their executions and material lots.
/// </summary>
public sealed class MaterialMovementConfiguration : IEntityTypeConfiguration<MaterialMovement>
{
    public void Configure(EntityTypeBuilder<MaterialMovement> builder)
    {
        builder.ToTable("material_movements", table =>
        {
            table.HasCheckConstraint("ck_material_movements_quantity", "\"Quantity\" > 0");
            table.HasCheckConstraint("ck_material_movements_quality", "(\"Kind\" = 'Output' AND \"OutputQuality\" IS NOT NULL) OR (\"Kind\" <> 'Output' AND \"OutputQuality\" IS NULL)");
        });

        builder.HasKey(movement => movement.Id);
        builder.Property(movement => movement.Id).HasMaxLength(100).ValueGeneratedNever();
        builder.Property(movement => movement.ExecutionId).HasMaxLength(100);
        builder.Property(movement => movement.MaterialLotId).HasMaxLength(100);
        builder.Property(movement => movement.UnitId).HasMaxLength(50);
        builder.Property(movement => movement.ReversesMovementId).HasMaxLength(100);
        builder.Property(movement => movement.Quantity).HasPrecision(18, 6);
        builder.Property(movement => movement.Kind).HasConversion<string>().HasMaxLength(32);
        builder.Property(movement => movement.OutputQuality).HasConversion<string>().HasMaxLength(32);

        builder.HasOne<Execution>().WithMany().HasForeignKey(movement => movement.ExecutionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<MaterialLot>().WithMany().HasForeignKey(movement => movement.MaterialLotId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<MaterialMovement>().WithMany().HasForeignKey(movement => movement.ReversesMovementId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(movement => new { movement.ExecutionId, movement.OccurredAt });
        builder.HasIndex(movement => new { movement.Kind, movement.OutputQuality, movement.OccurredAt });
    }
}
