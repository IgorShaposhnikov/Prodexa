using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prodexa.Core.Importing;

namespace Prodexa.Persistence.Configurations;

/// <summary>
/// Stores source revisions independently of their canonical manufacturing records.
/// </summary>
public sealed class SourceRecordConfiguration : IEntityTypeConfiguration<SourceRecord>
{
    public void Configure(EntityTypeBuilder<SourceRecord> builder)
    {
        builder.ToTable("source_records");

        builder.HasKey(record => new { record.SourceSystem, record.RecordType, record.SourceRecordId, record.SourceRevision });

        builder.Property(record => record.SourceSystem).HasMaxLength(100);
        builder.Property(record => record.RecordType).HasMaxLength(100);
        builder.Property(record => record.SourceRecordId).HasMaxLength(100);
        builder.Property(record => record.SourceRevision).HasMaxLength(100);
        builder.Property(record => record.TargetId).HasMaxLength(250);
        builder.Property(record => record.PayloadHash).HasMaxLength(64);

        builder.HasIndex(record => new { record.RecordType, record.TargetId });
    }
}
