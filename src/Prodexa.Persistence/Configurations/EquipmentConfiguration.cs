using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prodexa.Core.Resources;

namespace Prodexa.Persistence.Configurations;

/// <summary>
/// Maps stable equipment position identifiers.
/// </summary>
public sealed class EquipmentConfiguration : IEntityTypeConfiguration<Equipment>
{
    public void Configure(EntityTypeBuilder<Equipment> builder)
    {
        builder.ToTable("equipment");

        builder.HasKey(equipment => equipment.Id);
        builder.Property(equipment => equipment.Id).HasMaxLength(100).ValueGeneratedNever();
    }
}
