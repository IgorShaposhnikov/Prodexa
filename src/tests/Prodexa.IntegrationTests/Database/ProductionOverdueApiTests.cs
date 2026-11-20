using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Prodexa.Core.Materials;
using Prodexa.Core.Production;
using Prodexa.Production.Models;
using Xunit;

namespace Prodexa.IntegrationTests.Database;

/// <summary>
/// Verifies overdue production reports against real revisions and output events in PostgreSQL.
/// </summary>
[Collection("PostgreSQL")]
public sealed class ProductionOverdueApiTests(PostgreSqlFixture database)
{
    /// <summary>
    /// Only the current plan and accepted output of the planned product and unit count toward overdue work.
    /// </summary>
    [Fact]
    public async Task GetOverdueTasks_UsesEffectiveRevisionsAndAcceptedOutput()
    {
        var testId = Guid.NewGuid().ToString("N");
        var taskId = $"J-01-{testId}";
        var completedTaskId = $"J-02-{testId}";
        var futureTaskId = $"J-03-{testId}";
        var cancelledTaskId = $"J-04-{testId}";
        var rescheduledTaskId = $"J-05-{testId}";
        var futureRevisionTaskId = $"J-06-{testId}";
        var exactDeadlineTaskId = $"J-07-{testId}";
        var productId = $"PROD-A-{testId}";
        var otherProductId = $"PROD-B-{testId}";
        var asOf = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.FromHours(5));
        var firstRevisionStart = asOf.AddDays(-10);
        var secondRevisionStart = asOf.AddDays(-2);

        var earlierRevision = CreateRevision(taskId, 1, productId, 80m, asOf.AddHours(-3), firstRevisionStart, secondRevisionStart, PlanTaskStatus.Active);
        var effectiveRevision = CreateRevision(taskId, 2, productId, 100m, asOf.AddHours(-2), secondRevisionStart, null, PlanTaskStatus.Active);
        var completedRevision = CreateRevision(completedTaskId, 1, productId, 50m, asOf.AddHours(-2), firstRevisionStart, null, PlanTaskStatus.Completed);
        var futureRevision = CreateRevision(futureTaskId, 1, productId, 10m, asOf.AddHours(1), firstRevisionStart, null, PlanTaskStatus.Planned);
        var cancelledRevision = CreateRevision(cancelledTaskId, 1, productId, 10m, asOf.AddHours(-1), firstRevisionStart, null, PlanTaskStatus.Cancelled);
        var oldDeadlineRevision = CreateRevision(rescheduledTaskId, 1, productId, 10m, asOf.AddHours(-3), firstRevisionStart, asOf.AddDays(-1), PlanTaskStatus.Active);
        var rescheduledRevision = CreateRevision(rescheduledTaskId, 2, productId, 10m, asOf.AddDays(1), asOf.AddDays(-1), null, PlanTaskStatus.Active);
        var currentRevision = CreateRevision(futureRevisionTaskId, 1, productId, 10m, asOf.AddHours(-1), firstRevisionStart, asOf.AddDays(1), PlanTaskStatus.Active);
        var notYetActiveRevision = CreateRevision(futureRevisionTaskId, 2, productId, 10m, asOf.AddDays(3), asOf.AddDays(1), null, PlanTaskStatus.Active);
        var exactDeadlineRevision = CreateRevision(exactDeadlineTaskId, 1, productId, 10m, asOf, firstRevisionStart, null, PlanTaskStatus.Active);

        var earlyExecution = new Execution
        {
            Id = $"EX-01-{testId}",
            TaskId = taskId,
            TaskRevisionId = earlierRevision.Id,
            StartAt = asOf.AddDays(-3),
            Status = ExecutionStatus.Completed
        };
        var completedExecution = new Execution
        {
            Id = $"EX-02-{testId}",
            TaskId = completedTaskId,
            TaskRevisionId = completedRevision.Id,
            StartAt = asOf.AddDays(-1),
            Status = ExecutionStatus.Completed
        };

        var product = new MaterialDefinition { Id = productId };
        var otherProduct = new MaterialDefinition { Id = otherProductId };
        var productLot = CreateLot($"LOT-01-{testId}", productId, asOf.AddDays(-4));
        var otherProductLot = CreateLot($"LOT-02-{testId}", otherProductId, asOf.AddDays(-4));

        var acceptedOutput = CreateOutput(earlyExecution.Id, productLot.Id, 60m, asOf.AddMinutes(-30), OutputQuality.Good);
        var rejectedOutput = CreateOutput(earlyExecution.Id, productLot.Id, 20m, asOf.AddMinutes(-20), OutputQuality.Reject);
        var laterOutput = CreateOutput(earlyExecution.Id, productLot.Id, 10m, asOf.AddMinutes(5), OutputQuality.Good);
        var wrongUnitOutput = CreateOutput(earlyExecution.Id, productLot.Id, 5m, asOf.AddMinutes(-10), OutputQuality.Good, "kg");
        var otherProductOutput = CreateOutput(earlyExecution.Id, otherProductLot.Id, 7m, asOf.AddMinutes(-10), OutputQuality.Good);
        var completedOutput = CreateOutput(completedExecution.Id, productLot.Id, 50m, asOf.AddHours(-1), OutputQuality.Good);

        await using (var context = database.CreateContext())
        {
            var tasks = new[]
            {
                new PlanTask { Id = taskId },
                new PlanTask { Id = completedTaskId },
                new PlanTask { Id = futureTaskId },
                new PlanTask { Id = cancelledTaskId },
                new PlanTask { Id = rescheduledTaskId },
                new PlanTask { Id = futureRevisionTaskId },
                new PlanTask { Id = exactDeadlineTaskId }
            };

            context.PlanTasks.AddRange(tasks);
            context.PlanTaskRevisions.AddRange(
                earlierRevision, effectiveRevision, completedRevision, futureRevision, cancelledRevision,
                oldDeadlineRevision, rescheduledRevision, currentRevision, notYetActiveRevision, exactDeadlineRevision);
            context.Executions.AddRange(earlyExecution, completedExecution);
            context.MaterialDefinitions.AddRange(product, otherProduct);
            context.MaterialLots.AddRange(productLot, otherProductLot);
            context.MaterialMovements.AddRange(acceptedOutput, rejectedOutput, laterOutput, wrongUnitOutput, otherProductOutput, completedOutput);

            await context.SaveChangesAsync();
        }

        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var parameter = Uri.EscapeDataString(asOf.ToString("O"));

        var response = await client.GetAsync($"/api/production/overdue-tasks?asOf={parameter}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var reports = await response.Content.ReadFromJsonAsync<List<OverdueProductionTask>>();

        Assert.NotNull(reports);

        var overdue = Assert.Single(reports.Where(report => report.TaskId == taskId));

        Assert.Equal(effectiveRevision.Id, overdue.RevisionId);
        Assert.Equal(2, overdue.RevisionNumber);
        Assert.Equal(100m, overdue.PlannedQuantity);
        Assert.Equal(60m, overdue.AcceptedGoodOutput);
        Assert.Equal(40m, overdue.RemainingQuantity);
        Assert.Equal(120m, overdue.OverdueMinutes);
        Assert.Equal(asOf.ToUniversalTime(), overdue.AsOf);

        Assert.Contains(reports, report => report.TaskId == futureRevisionTaskId && report.RevisionId == currentRevision.Id);
        Assert.DoesNotContain(reports, report => report.TaskId == completedTaskId);
        Assert.DoesNotContain(reports, report => report.TaskId == futureTaskId);
        Assert.DoesNotContain(reports, report => report.TaskId == cancelledTaskId);
        Assert.DoesNotContain(reports, report => report.TaskId == rescheduledTaskId);
        Assert.DoesNotContain(reports, report => report.TaskId == exactDeadlineTaskId);
    }

    /// <summary>
    /// A future plan revision must not replace the revision effective at the historical reporting time.
    /// </summary>
    [Fact]
    public async Task GetOverdueTasks_UsesHistoricalRevisionAsOfReportingTime()
    {
        var taskId = $"J-HISTORY-{Guid.NewGuid():N}";
        var asOf = new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.FromHours(5));
        var currentRevision = CreateRevision(taskId, 1, "PROD-H", 25m, asOf.AddHours(-1), asOf.AddDays(-5), asOf.AddDays(1), PlanTaskStatus.Active);
        var futureRevision = CreateRevision(taskId, 2, "PROD-H", 30m, asOf.AddDays(2), asOf.AddDays(1), null, PlanTaskStatus.Active);

        await using (var context = database.CreateContext())
        {
            var task = new PlanTask { Id = taskId };

            context.PlanTasks.Add(task);
            context.PlanTaskRevisions.AddRange(currentRevision, futureRevision);

            await context.SaveChangesAsync();
        }

        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var parameter = Uri.EscapeDataString(asOf.ToString("O"));

        var response = await client.GetAsync($"/api/production/overdue-tasks?asOf={parameter}");
        var reports = await response.Content.ReadFromJsonAsync<List<OverdueProductionTask>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(reports);

        var overdue = Assert.Single(reports.Where(report => report.TaskId == taskId));
        Assert.Equal(currentRevision.Id, overdue.RevisionId);
        Assert.Equal(25m, overdue.RemainingQuantity);
        Assert.Equal(60m, overdue.OverdueMinutes);
    }

    /// <summary>
    /// The caller must provide a valid reporting instant.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("?asOf=invalid")]
    public async Task GetOverdueTasks_RejectsMissingOrInvalidReportingTime(string query)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/production/overdue-tasks{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private WebApplicationFactory<Program> CreateFactory()
    {
        var connectionString = Environment.GetEnvironmentVariable("PRODEXA_TEST_CONNECTION_STRING");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("PRODEXA_TEST_CONNECTION_STRING must point to a dedicated test database.");
        }

        return new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            host.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Prodexa"] = connectionString
                });
            });
        });
    }

    private PlanTaskRevision CreateRevision(
        string taskId,
        int revisionNumber,
        string productId,
        decimal plannedQuantity,
        DateTimeOffset dueAt,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveTo,
        PlanTaskStatus status)
    {
        return new PlanTaskRevision
        {
            Id = $"{taskId}-v{revisionNumber}",
            TaskId = taskId,
            RevisionNumber = revisionNumber,
            ProductId = productId,
            ProcessSegmentVersionId = "SEG-A-v1",
            PlannedQuantity = plannedQuantity,
            UnitId = "piece",
            DueAt = dueAt,
            Status = status,
            EffectiveFrom = effectiveFrom,
            EffectiveTo = effectiveTo
        };
    }

    private MaterialLot CreateLot(string id, string materialId, DateTimeOffset createdAt)
    {
        return new MaterialLot
        {
            Id = id,
            MaterialId = materialId,
            CreatedAt = createdAt,
            Status = "Active",
            UnitId = "piece"
        };
    }

    private MaterialMovement CreateOutput(
        string executionId,
        string lotId,
        decimal quantity,
        DateTimeOffset occurredAt,
        OutputQuality quality,
        string unitId = "piece")
    {
        return new MaterialMovement
        {
            Id = $"MOV-{Guid.NewGuid():N}",
            ExecutionId = executionId,
            MaterialLotId = lotId,
            Kind = MaterialMovementKind.Output,
            Quantity = quantity,
            UnitId = unitId,
            OccurredAt = occurredAt,
            OutputQuality = quality
        };
    }
}
