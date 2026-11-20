using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prodexa.Core.Production;

namespace Prodexa.Persistence.Configurations;

/// <summary>
/// Maps production executions to the exact plan revision used at execution time.
/// </summary>
public sealed class ExecutionConfiguration : IEntityTypeConfiguration<Execution>
{
    public void Configure(EntityTypeBuilder<Execution> builder)
    {
        builder.ToTable("executions", table =>
        {
            table.HasCheckConstraint("ck_executions_interval", "\"EndAt\" IS NULL OR \"EndAt\" >= \"StartAt\"");
        });

        builder.HasKey(execution => execution.Id);
        builder.Property(execution => execution.Id).HasMaxLength(100).ValueGeneratedNever();
        builder.Property(execution => execution.TaskId).HasMaxLength(100);
        builder.Property(execution => execution.TaskRevisionId).HasMaxLength(100);
        builder.Property(execution => execution.Status).HasConversion<string>().HasMaxLength(32);

        // The composite foreign key prevents mixing a task with another task's revision.
        builder.HasOne<PlanTaskRevision>().WithMany()
            .HasForeignKey(execution => new { execution.TaskId, execution.TaskRevisionId })
            .HasPrincipalKey(revision => new { revision.TaskId, revision.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(execution => execution.StartAt);
    }
}
