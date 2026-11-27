using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Prodexa.Core.Resources;
using EquipmentResource = Prodexa.Core.Resources.Equipment;
using Prodexa.Equipment.Models;
using Xunit;

namespace Prodexa.IntegrationTests.Database;

/// <summary>
/// Verifies downtime analysis through the equipment HTTP module and real PostgreSQL data.
/// </summary>
[Collection("PostgreSQL")]
public sealed class EquipmentAnalysisApiTests(PostgreSqlFixture database)
{
    /// <summary>
    /// The reporting window clips overlapping events before their durations are combined.
    /// </summary>
    [Fact]
    public async Task GetDowntime_MergesOverlappingEventsWithoutDoubleCounting()
    {
        var equipmentId = $"EQ-{Guid.NewGuid():N}";
        var otherEquipmentId = $"EQ-{Guid.NewGuid():N}";

        await using (var context = database.CreateContext())
        {
            var equipment = new EquipmentResource { Id = equipmentId };
            var otherEquipment = new EquipmentResource { Id = otherEquipmentId };
            var firstDowntime = CreateDowntime(equipmentId, At(7, 50), At(8, 20));
            var overlappingDowntime = CreateDowntime(equipmentId, At(8, 10), At(9, 0));
            var otherEquipmentDowntime = CreateDowntime(otherEquipmentId, At(8, 0), At(10, 0));
            var outsidePeriodDowntime = CreateDowntime(equipmentId, At(12, 0), At(13, 0));

            context.Equipment.AddRange(equipment, otherEquipment);
            context.Downtimes.AddRange(firstDowntime, overlappingDowntime, otherEquipmentDowntime, outsidePeriodDowntime);

            await context.SaveChangesAsync();
        }

        var response = await GetReportAsync(equipmentId, At(8, 0), At(12, 0));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var report = await response.Content.ReadFromJsonAsync<EquipmentDowntimeReport>();

        Assert.NotNull(report);
        Assert.Equal(equipmentId, report.EquipmentId);
        Assert.Equal(At(8, 0), report.PeriodStart);
        Assert.Equal(At(12, 0), report.PeriodEnd);
        Assert.Equal(2, report.EventCount);
        Assert.Equal(60m, report.DowntimeMinutes);
        Assert.Equal(60m, report.UnplannedDowntimeMinutes);
        Assert.Equal(0m, report.PlannedDowntimeMinutes);
        Assert.Equal(75m, report.AvailabilityPercent);
    }

    /// <summary>
    /// Planned and unplanned durations remain separate when their intervals overlap.
    /// </summary>
    [Fact]
    public async Task GetDowntime_SeparatesPlannedAndUnplannedIntervals()
    {
        var equipmentId = $"EQ-{Guid.NewGuid():N}";

        await using (var context = database.CreateContext())
        {
            var equipment = new EquipmentResource { Id = equipmentId };
            var plannedDowntime = new Downtime
            {
                Id = $"D-{Guid.NewGuid():N}",
                EquipmentId = equipmentId,
                StartAt = At(8, 30),
                EndAt = At(10, 0),
                ReasonCode = "MAINTENANCE",
                IsPlanned = true
            };

            var firstDowntime = CreateDowntime(equipmentId, At(7, 50), At(8, 20));
            var overlappingDowntime = CreateDowntime(equipmentId, At(8, 10), At(9, 0));

            context.Equipment.Add(equipment);
            context.Downtimes.AddRange(plannedDowntime, firstDowntime, overlappingDowntime);
            await context.SaveChangesAsync();
        }

        var response = await GetReportAsync(equipmentId, At(8, 0), At(12, 0));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var report = await response.Content.ReadFromJsonAsync<EquipmentDowntimeReport>();
        Assert.NotNull(report);
        Assert.Equal(3, report.EventCount);
        Assert.Equal(120m, report.DowntimeMinutes);
        Assert.Equal(90m, report.PlannedDowntimeMinutes);
        Assert.Equal(60m, report.UnplannedDowntimeMinutes);
        Assert.Equal(50m, report.AvailabilityPercent);
    }

    /// <summary>
    /// Open downtime is limited by the report end and overlaps are not double-counted.
    /// </summary>
    [Fact]
    public async Task GetDowntime_BoundsOpenEventsAndCountsTheirUnion()
    {
        var equipmentId = $"EQ-{Guid.NewGuid():N}";

        await using (var context = database.CreateContext())
        {
            var equipment = new EquipmentResource { Id = equipmentId };
            var openDowntime = CreateDowntime(equipmentId, At(9, 0), null);
            var overlappingDowntime = CreateDowntime(equipmentId, At(9, 15), At(10, 0));

            context.Equipment.Add(equipment);
            context.Downtimes.AddRange(openDowntime, overlappingDowntime);

            await context.SaveChangesAsync();
        }

        var response = await GetReportAsync(equipmentId, At(9, 0), At(10, 0));
        var report = await response.Content.ReadFromJsonAsync<EquipmentDowntimeReport>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(report);
        Assert.Equal(2, report.EventCount);
        Assert.Equal(60m, report.DowntimeMinutes);
        Assert.Equal(60m, report.UnplannedDowntimeMinutes);
        Assert.Equal(0m, report.PlannedDowntimeMinutes);
        Assert.Equal(0m, report.AvailabilityPercent);
    }

    /// <summary>
    /// Equipment without downtime is fully available over the reporting period.
    /// </summary>
    [Fact]
    public async Task GetDowntime_ReportsFullAvailabilityWithoutEvents()
    {
        var equipmentId = $"EQ-{Guid.NewGuid():N}";

        await using (var context = database.CreateContext())
        {
            var equipment = new EquipmentResource { Id = equipmentId };
            context.Equipment.Add(equipment);
            await context.SaveChangesAsync();
        }

        var response = await GetReportAsync(equipmentId, At(8, 0), At(12, 0));
        var report = await response.Content.ReadFromJsonAsync<EquipmentDowntimeReport>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(report);
        Assert.Equal(0, report.EventCount);
        Assert.Equal(0m, report.DowntimeMinutes);
        Assert.Equal(0m, report.UnplannedDowntimeMinutes);
        Assert.Equal(0m, report.PlannedDowntimeMinutes);
        Assert.Equal(100m, report.AvailabilityPercent);
    }

    /// <summary>
    /// Unknown equipment IDs do not produce misleading empty reports.
    /// </summary>
    [Fact]
    public async Task GetDowntime_ReturnsNotFoundForUnknownEquipment()
    {
        var response = await GetReportAsync($"EQ-{Guid.NewGuid():N}", At(8, 0), At(12, 0));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Invalid reporting windows are rejected before executing database queries.
    /// </summary>
    [Theory]
    [InlineData(12, 8)]
    [InlineData(8, 8)]
    public async Task GetDowntime_RejectsInvalidReportingWindow(int startHour, int endHour)
    {
        var response = await GetReportAsync("EQ-ANY", At(startHour, 0), At(endHour, 0));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Both boundaries must be explicitly provided by the API caller.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("?periodStart=2026-11-01T08%3A00%3A00%2B05%3A00")]
    [InlineData("?periodEnd=2026-11-01T12%3A00%3A00%2B05%3A00")]
    public async Task GetDowntime_RequiresBothWindowBoundaries(string query)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/equipment/EQ-ANY/downtime{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<HttpResponseMessage> GetReportAsync(string equipmentId, DateTimeOffset periodStart, DateTimeOffset periodEnd)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var startParameter = Uri.EscapeDataString(periodStart.ToString("O"));
        var endParameter = Uri.EscapeDataString(periodEnd.ToString("O"));

        return await client.GetAsync($"/api/equipment/{equipmentId}/downtime?periodStart={startParameter}&periodEnd={endParameter}");
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

    private DateTimeOffset At(int hour, int minute)
    {
        return new DateTimeOffset(2026, 11, 1, hour, minute, 0, TimeSpan.FromHours(5));
    }

    private Downtime CreateDowntime(string equipmentId, DateTimeOffset start, DateTimeOffset? end)
    {
        return new Downtime
        {
            Id = $"D-{Guid.NewGuid():N}",
            EquipmentId = equipmentId,
            StartAt = start,
            EndAt = end,
            ReasonCode = "MAINTENANCE",
            IsPlanned = false
        };
    }
}
