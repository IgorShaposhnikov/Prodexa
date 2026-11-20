using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prodexa.Core.Materials;

namespace Prodexa.Persistence.Configurations;

/// <summary>
/// Maps stable material identifiers shared by source lots and production plans.
/// </summary>
public sealed class MaterialDefinitionConfiguration : IEntityTypeConfiguration<MaterialDefinition>
{
    public void Configure(EntityTypeBuilder<MaterialDefinition> builder)
    {
        builder.ToTable("material_definitions");

        builder.HasKey(material => material.Id);
        builder.Property(material => material.Id).HasMaxLength(100).ValueGeneratedNever();
    }
}
