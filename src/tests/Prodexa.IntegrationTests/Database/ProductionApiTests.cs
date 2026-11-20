using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Prodexa.Core.Production;
using Prodexa.Production.Models;
using Xunit;

namespace Prodexa.IntegrationTests.Database;

/// <summary>
/// Verifies module routing, dependency injection, and production queries over HTTP.
/// </summary>
[Collection("PostgreSQL")]
public sealed class ProductionApiTests(PostgreSqlFixture database)
{
    /// <summary>
    /// The production module exposes the complete revision history of a task in revision order.
    /// </summary>
    [Fact]
    public async Task GetTask_ReturnsOrderedPlanRevisions()
    {
        var taskId = $"J-{Guid.NewGuid():N}";
        var effectiveFrom = new DateTimeOffset(2026, 11, 1, 8, 0, 0, TimeSpan.FromHours(5));
        var nextRevisionFrom = effectiveFrom.AddDays(1);

        await using (var context = database.CreateContext())
        {
            var plannedTask = new PlanTask { Id = taskId };
            var firstRevision = CreateRevision($"{taskId}-v1", taskId, 1, effectiveFrom, nextRevisionFrom, PlanTaskStatus.Planned);
            var secondRevision = CreateRevision($"{taskId}-v2", taskId, 2, nextRevisionFrom, null, PlanTaskStatus.Active);

            context.PlanTasks.Add(plannedTask);
            context.PlanTaskRevisions.AddRange(firstRevision, secondRevision);

            await context.SaveChangesAsync();
        }

        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/production/tasks/{taskId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var task = await response.Content.ReadFromJsonAsync<ProductionTaskDetails>();

        Assert.NotNull(task);
        Assert.Equal(taskId, task.Id);
        Assert.Equal(2, task.Revisions.Count);
        Assert.Equal(1, task.Revisions[0].RevisionNumber);
        Assert.Equal(2, task.Revisions[1].RevisionNumber);
        Assert.Equal("Active", task.Revisions[1].Status);
        Assert.Equal(TimeSpan.Zero, task.Revisions[1].DueAt.Offset);
    }

    /// <summary>
    /// The list endpoint returns tasks and their revision counts from PostgreSQL.
    /// </summary>
    [Fact]
    public async Task GetTasks_ReturnsRevisionCount()
    {
        var taskId = $"J-{Guid.NewGuid():N}";
        var effectiveFrom = new DateTimeOffset(2026, 11, 1, 8, 0, 0, TimeSpan.FromHours(5));

        await using (var context = database.CreateContext())
        {
            var plannedTask = new PlanTask { Id = taskId };
            var revision = CreateRevision($"{taskId}-v1", taskId, 1, effectiveFrom, null, PlanTaskStatus.Active);

            context.PlanTasks.Add(plannedTask);
            context.PlanTaskRevisions.Add(revision);

            await context.SaveChangesAsync();
        }

        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/production/tasks?offset=0&limit=100");
        var tasks = await response.Content.ReadFromJsonAsync<List<ProductionTaskSummary>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(tasks);
        Assert.Contains(tasks, task => task.Id == taskId && task.RevisionCount == 1);
    }

    /// <summary>
    /// The API returns 404 when the requested task does not exist.
    /// </summary>
    [Fact]
    public async Task GetTask_ReturnsNotFoundForUnknownTask()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/production/tasks/J-{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// The list endpoint rejects invalid pagination before accessing the database.
    /// </summary>
    [Theory]
    [InlineData(-1, 50)]
    [InlineData(0, 0)]
    [InlineData(0, 101)]
    public async Task GetTasks_RejectsInvalidPagination(int offset, int limit)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/production/tasks?offset={offset}&limit={limit}");

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

    private PlanTaskRevision CreateRevision(string id, string taskId, int number, DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo, PlanTaskStatus status)
    {
        return new PlanTaskRevision
        {
            Id = id,
            TaskId = taskId,
            RevisionNumber = number,
            ProductId = "PROD-A",
            ProcessSegmentVersionId = "SEG-A-v1",
            PlannedQuantity = 100m,
            UnitId = "piece",
            DueAt = effectiveFrom.AddDays(3),
            Status = status,
            EffectiveFrom = effectiveFrom,
            EffectiveTo = effectiveTo
        };
    }
}
