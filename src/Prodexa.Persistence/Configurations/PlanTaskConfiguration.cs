using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prodexa.Core.Production;

namespace Prodexa.Persistence.Configurations;

/// <summary>
/// Maps the stable identity of a production task.
/// </summary>
public sealed class PlanTaskConfiguration : IEntityTypeConfiguration<PlanTask>
{
    public void Configure(EntityTypeBuilder<PlanTask> builder)
    {
        builder.ToTable("plan_tasks");

        builder.HasKey(task => task.Id);
        builder.Property(task => task.Id).HasMaxLength(100).ValueGeneratedNever();
    }
}
