using Microsoft.EntityFrameworkCore;
using Npgsql;
using Prodexa.Core.Production;
using Prodexa.Core.Resources;
using EquipmentResource = Prodexa.Core.Resources.Equipment;
using Xunit;

namespace Prodexa.IntegrationTests.Database;

/// <summary>
/// Verifies production persistence against a real PostgreSQL database.
/// </summary>
[Collection("PostgreSQL")]
public sealed class PostgreSqlPersistenceTests(PostgreSqlFixture database)
{
    /// <summary>
    /// Confirms that the initial migration is registered by PostgreSQL.
    /// </summary>
    [Fact]
    public async Task Migrations_ApplyInitialSchema()
    {
        await using var context = database.CreateContext();

        var migrations = await context.Database.GetAppliedMigrationsAsync();

        Assert.Contains(migrations, name => name.EndsWith("_InitialCreate", StringComparison.Ordinal));
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
    }

    /// <summary>
    /// Saves a task, its revision and an execution while converting source timestamps to UTC.
    /// </summary>
    [Fact]
    public async Task ProductionRecords_RoundTripUsingUtc()
    {
        var id = Guid.NewGuid().ToString("N");
        var taskId = $"J-{id}";
        var revisionId = $"J-{id}-v1";
        var executionId = $"EX-{id}";
        var dueAt = new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.FromHours(5));
        var executionStart = new DateTimeOffset(2026, 11, 1, 8, 0, 0, TimeSpan.FromHours(5));

        await using (var context = database.CreateContext())
        {
            var task = new PlanTask { Id = taskId };
            context.PlanTasks.Add(task);
            var revision = new PlanTaskRevision
            {
                Id = revisionId,
                TaskId = taskId,
                RevisionNumber = 1,
                ProductId = "PROD-A",
                ProcessSegmentVersionId = "SEG-A-v1",
                PlannedQuantity = 100m,
                UnitId = "piece",
                DueAt = dueAt,
                Status = PlanTaskStatus.Active,
                EffectiveFrom = dueAt.AddDays(-1)
            };
            context.PlanTaskRevisions.Add(revision);
            var execution = new Execution
            {
                Id = executionId,
                TaskId = taskId,
                TaskRevisionId = revisionId,
                StartAt = executionStart,
                Status = ExecutionStatus.Started
            };
            context.Executions.Add(execution);

            await context.SaveChangesAsync();
        }

        await using (var context = database.CreateContext())
        {
            var revision = await context.PlanTaskRevisions.AsNoTracking().SingleAsync(item => item.Id == revisionId);
            var execution = await context.Executions.AsNoTracking().SingleAsync(item => item.Id == executionId);

            Assert.Equal(TimeSpan.Zero, revision.DueAt.Offset);
            Assert.Equal(dueAt.ToUniversalTime(), revision.DueAt);
            Assert.Equal(TimeSpan.Zero, execution.StartAt.Offset);
            Assert.Equal(executionStart.ToUniversalTime(), execution.StartAt);
            Assert.Equal(PlanTaskStatus.Active, revision.Status);
            Assert.Equal(ExecutionStatus.Started, execution.Status);
        }
    }

    /// <summary>
    /// A plan revision belonging to another task cannot be referenced by an execution.
    /// </summary>
    [Fact]
    public async Task Execution_RejectsRevisionFromAnotherTask()
    {
        var id = Guid.NewGuid().ToString("N");
        var ownTaskId = $"J-OWN-{id}";
        var otherTaskId = $"J-OTHER-{id}";
        var otherRevisionId = $"R-OTHER-{id}";
        var dueAt = new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.FromHours(5));

        await using (var context = database.CreateContext())
        {
            var ownTask = new PlanTask { Id = ownTaskId };
            var otherTask = new PlanTask { Id = otherTaskId };
            context.PlanTasks.AddRange(ownTask, otherTask);
            var revision = new PlanTaskRevision
            {
                Id = otherRevisionId,
                TaskId = otherTaskId,
                RevisionNumber = 1,
                ProductId = "PROD-A",
                ProcessSegmentVersionId = "SEG-A-v1",
                PlannedQuantity = 10m,
                UnitId = "piece",
                DueAt = dueAt,
                Status = PlanTaskStatus.Active,
                EffectiveFrom = dueAt.AddDays(-1)
            };
            context.PlanTaskRevisions.Add(revision);
            await context.SaveChangesAsync();
        }

        await using (var context = database.CreateContext())
        {
            var execution = new Execution
            {
                Id = $"EX-{id}",
                TaskId = ownTaskId,
                TaskRevisionId = otherRevisionId,
                StartAt = dueAt.AddHours(-1),
                Status = ExecutionStatus.Started
            };
            context.Executions.Add(execution);

            var exception = await Assert.ThrowsAsync<DbUpdateException>(async () => await context.SaveChangesAsync());
            var postgresException = Assert.IsType<PostgresException>(exception.InnerException);

            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, postgresException.SqlState);
        }
    }

    /// <summary>
    /// Persists downtime against an equipment position with normalized timestamps.
    /// </summary>
    [Fact]
    public async Task Downtime_RoundTripsWithEquipment()
    {
        var id = Guid.NewGuid().ToString("N");
        var equipmentId = $"EQ-{id}";
        var downtimeId = $"D-{id}";
        var start = new DateTimeOffset(2026, 11, 1, 7, 50, 0, TimeSpan.FromHours(5));

        await using (var context = database.CreateContext())
        {
            var equipment = new EquipmentResource { Id = equipmentId };
        context.Equipment.Add(equipment);
            var downtime = new Downtime
            {
                Id = downtimeId,
                EquipmentId = equipmentId,
                StartAt = start,
                EndAt = start.AddMinutes(30),
                ReasonCode = "MECHANICAL",
                IsPlanned = false
            };
            context.Downtimes.Add(downtime);
            await context.SaveChangesAsync();
        }

        await using (var context = database.CreateContext())
        {
            var stored = await context.Downtimes.AsNoTracking().SingleAsync(item => item.Id == downtimeId);

            Assert.Equal(equipmentId, stored.EquipmentId);
            Assert.Equal(start.ToUniversalTime(), stored.StartAt);
            Assert.Equal(TimeSpan.Zero, stored.StartAt.Offset);
            Assert.Equal(TimeSpan.FromMinutes(30), stored.EndAt - stored.StartAt);
        }
    }

    /// <summary>
    /// PostgreSQL rejects negative downtime intervals independently of application validation.
    /// </summary>
    [Fact]
    public async Task Downtime_RejectsInvalidInterval()
    {
        var id = Guid.NewGuid().ToString("N");
        var equipmentId = $"EQ-{id}";
        var start = new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.FromHours(5));

        await using var context = database.CreateContext();
        var equipment = new EquipmentResource { Id = equipmentId };
            context.Equipment.Add(equipment);
        var downtime = new Downtime
        {
            Id = $"D-{id}",
            EquipmentId = equipmentId,
            StartAt = start,
            EndAt = start.AddMinutes(-1),
            ReasonCode = "MECHANICAL",
            IsPlanned = false
        };
        context.Downtimes.Add(downtime);

        var exception = await Assert.ThrowsAsync<DbUpdateException>(async () => await context.SaveChangesAsync());
        var postgresException = Assert.IsType<PostgresException>(exception.InnerException);

        Assert.Equal(PostgresErrorCodes.CheckViolation, postgresException.SqlState);
    }
}
