using Microsoft.EntityFrameworkCore;
using Prodexa.Core.Production;
using Prodexa.Core.Resources;
using Prodexa.Persistence;
using Xunit;

namespace Prodexa.Tests.Persistence;

/// <summary>
/// Verifies the relational model without requiring a running PostgreSQL server.
/// </summary>
public sealed class ProdexaDbContextTests
{
    /// <summary>
    /// Each persisted production entity has a dedicated table.
    /// </summary>
    [Theory]
    [InlineData(typeof(PlanTask), "plan_tasks")]
    [InlineData(typeof(PlanTaskRevision), "plan_task_revisions")]
    [InlineData(typeof(Execution), "executions")]
    [InlineData(typeof(Equipment), "equipment")]
    [InlineData(typeof(Downtime), "downtimes")]
    public void Model_MapsProductionEntities(Type entityType, string expectedTable)
    {
        using var context = CreateContext();

        var mapping = context.Model.FindEntityType(entityType);

        Assert.NotNull(mapping);
        Assert.Equal(expectedTable, mapping.GetTableName());
    }

    /// <summary>
    /// An execution must reference a revision belonging to the specified task.
    /// </summary>
    [Fact]
    public void Model_ExecutionUsesTaskAndRevisionAsForeignKey()
    {
        using var context = CreateContext();

        var execution = context.Model.FindEntityType(typeof(Execution));

        Assert.NotNull(execution);

        var foreignKey = Assert.Single(execution.GetForeignKeys());

        Assert.Equal(typeof(PlanTaskRevision), foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal(
            new[] { nameof(Execution.TaskId), nameof(Execution.TaskRevisionId) },
            foreignKey.Properties.Select(property => property.Name));
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
    }

    /// <summary>
    /// Task revision numbers must be unique within each task.
    /// </summary>
    [Fact]
    public void Model_TaskRevisionHasUniqueRevisionIndex()
    {
        using var context = CreateContext();

        var revision = context.Model.FindEntityType(typeof(PlanTaskRevision));

        Assert.NotNull(revision);

        Assert.Contains(revision.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(
                new[] { nameof(PlanTaskRevision.TaskId), nameof(PlanTaskRevision.RevisionNumber) }));
    }

    /// <summary>
    /// Downtime is associated with equipment and is indexed by its start time.
    /// </summary>
    [Fact]
    public void Model_DowntimeHasEquipmentRelationAndLookupIndex()
    {
        using var context = CreateContext();

        var downtime = context.Model.FindEntityType(typeof(Downtime));

        Assert.NotNull(downtime);

        var foreignKey = Assert.Single(downtime.GetForeignKeys());

        Assert.Equal(typeof(Equipment), foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal(nameof(Downtime.EquipmentId), Assert.Single(foreignKey.Properties).Name);

        Assert.Contains(downtime.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual(
                new[] { nameof(Downtime.EquipmentId), nameof(Downtime.StartAt) }));
    }

    /// <summary>
    /// All mapped source timestamps must be converted to UTC for PostgreSQL.
    /// </summary>
    [Theory]
    [InlineData(typeof(PlanTaskRevision), nameof(PlanTaskRevision.DueAt))]
    [InlineData(typeof(Execution), nameof(Execution.StartAt))]
    [InlineData(typeof(Downtime), nameof(Downtime.StartAt))]
    public void Model_NormalizesSourceTimeToUtc(Type entityType, string propertyName)
    {
        using var context = CreateContext();

        var property = context.Model.FindEntityType(entityType)?.FindProperty(propertyName);

        Assert.NotNull(property);

        var converter = property.GetTypeMapping().Converter;

        Assert.NotNull(converter);

        var sourceTime = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.FromHours(5));
        var storedTime = Assert.IsType<DateTimeOffset>(converter.ConvertToProvider(sourceTime));

        Assert.Equal(TimeSpan.Zero, storedTime.Offset);
        Assert.Equal(sourceTime.ToUniversalTime(), storedTime);
    }

    private ProdexaDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ProdexaDbContext>()
            .UseNpgsql("Host=localhost;Database=prodexa;Username=prodexa;Password=unused")
            .Options;

        return new ProdexaDbContext(options);
    }
}
