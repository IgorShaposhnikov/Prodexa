using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prodexa.Core.Production;

namespace Prodexa.Persistence.Configurations;

/// <summary>
/// Maps historical plan revisions and their relationship to production tasks.
/// </summary>
public sealed class PlanTaskRevisionConfiguration : IEntityTypeConfiguration<PlanTaskRevision>
{
    public void Configure(EntityTypeBuilder<PlanTaskRevision> builder)
    {
        builder.ToTable("plan_task_revisions", table =>
        {
            table.HasCheckConstraint("ck_plan_task_revisions_quantity", "\"PlannedQuantity\" >= 0");
            table.HasCheckConstraint("ck_plan_task_revisions_revision", "\"RevisionNumber\" > 0");
            table.HasCheckConstraint("ck_plan_task_revisions_period", "\"EffectiveTo\" IS NULL OR \"EffectiveTo\" > \"EffectiveFrom\"");
        });

        builder.HasKey(revision => revision.Id);
        builder.HasAlternateKey(revision => new { revision.TaskId, revision.Id });

        builder.Property(revision => revision.Id).HasMaxLength(100).ValueGeneratedNever();
        builder.Property(revision => revision.TaskId).HasMaxLength(100);
        builder.Property(revision => revision.ProductId).HasMaxLength(100);
        builder.Property(revision => revision.ProcessSegmentVersionId).HasMaxLength(100);
        builder.Property(revision => revision.UnitId).HasMaxLength(50);
        builder.Property(revision => revision.PlannedQuantity).HasPrecision(18, 6);
        builder.Property(revision => revision.Status).HasConversion<string>().HasMaxLength(32);

        builder.HasIndex(revision => new { revision.TaskId, revision.RevisionNumber }).IsUnique();
        builder.HasIndex(revision => revision.DueAt);

        builder.HasOne<PlanTask>().WithMany().HasForeignKey(revision => revision.TaskId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
