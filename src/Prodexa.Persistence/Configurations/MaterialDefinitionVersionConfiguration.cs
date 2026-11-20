using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prodexa.Core.Materials;

namespace Prodexa.Persistence.Configurations;

/// <summary>
/// Maps versioned material descriptions used by consumption norms.
/// </summary>
public sealed class MaterialDefinitionVersionConfiguration : IEntityTypeConfiguration<MaterialDefinitionVersion>
{
    public void Configure(EntityTypeBuilder<MaterialDefinitionVersion> builder)
    {
        builder.ToTable("material_definition_versions", table =>
        {
            table.HasCheckConstraint("ck_material_definition_versions_revision", "\"VersionNumber\" > 0");
            table.HasCheckConstraint("ck_material_definition_versions_period", "\"ValidTo\" IS NULL OR \"ValidTo\" > \"ValidFrom\"");
        });

        builder.HasKey(version => version.Id);
        builder.Property(version => version.Id).HasMaxLength(100).ValueGeneratedNever();
        builder.Property(version => version.MaterialId).HasMaxLength(100);
        builder.Property(version => version.Name).HasMaxLength(200);
        builder.Property(version => version.MaterialClassId).HasMaxLength(100);
        builder.Property(version => version.BaseUnitId).HasMaxLength(50);
        builder.Property(version => version.SourceRecordId).HasMaxLength(100);

        builder.HasOne<MaterialDefinition>().WithMany().HasForeignKey(version => version.MaterialId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(version => new { version.MaterialId, version.VersionNumber }).IsUnique();
    }
}
