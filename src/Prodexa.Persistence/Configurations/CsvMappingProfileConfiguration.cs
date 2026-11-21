using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prodexa.Core.Importing;

namespace Prodexa.Persistence.Configurations;

/// <summary>
/// Persists user-defined CSV mapping profiles in PostgreSQL.
/// </summary>
public sealed class CsvMappingProfileConfiguration : IEntityTypeConfiguration<CsvMappingProfile>
{
    public void Configure(EntityTypeBuilder<CsvMappingProfile> builder)
    {
        builder.ToTable("csv_mapping_profiles");
        builder.HasKey(profile => profile.Id);
        builder.Property(profile => profile.Id).HasMaxLength(100).ValueGeneratedNever();
        builder.Property(profile => profile.Name).HasMaxLength(200);
        builder.Property(profile => profile.SourceSystem).HasMaxLength(100);
        builder.Property(profile => profile.Target).HasMaxLength(100);
        builder.Property(profile => profile.DefinitionJson).HasColumnType("jsonb");
    }
}
