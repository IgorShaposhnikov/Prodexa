using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prodexa.Core.Resources;

namespace Prodexa.Persistence.Configurations;

/// <summary>
/// Maps downtime intervals and their equipment relationships.
/// </summary>
public sealed class DowntimeConfiguration : IEntityTypeConfiguration<Downtime>
{
    public void Configure(EntityTypeBuilder<Downtime> builder)
    {
        builder.ToTable("downtimes", table =>
        {
            table.HasCheckConstraint("ck_downtimes_interval", "\"EndAt\" IS NULL OR \"EndAt\" >= \"StartAt\"");
        });

        builder.HasKey(downtime => downtime.Id);
        builder.Property(downtime => downtime.Id).HasMaxLength(100).ValueGeneratedNever();
        builder.Property(downtime => downtime.EquipmentId).HasMaxLength(100);
        builder.Property(downtime => downtime.ReasonCode).HasMaxLength(100);
        builder.Property(downtime => downtime.Comment).HasMaxLength(2000);

        builder.HasOne<Equipment>().WithMany().HasForeignKey(downtime => downtime.EquipmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(downtime => new { downtime.EquipmentId, downtime.StartAt });
    }
}
