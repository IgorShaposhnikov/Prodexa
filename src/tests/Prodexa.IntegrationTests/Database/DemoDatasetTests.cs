using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Prodexa.Equipment.Models;
using Prodexa.Importing.Models;
using Prodexa.Materials.Models;
using Prodexa.Production.Models;
using Xunit;

namespace Prodexa.IntegrationTests.Database;

/// <summary>
/// Verifies that the published demo data reproduces all three manufacturing analytics scenarios.
/// </summary>
[Collection("PostgreSQL")]
public sealed class DemoDatasetTests(PostgreSqlFixture database)
{
    /// <summary>
    /// Imports the sample through HTTP, verifies idempotency and checks the production reports.
    /// </summary>
    [Fact]
    public async Task DemoData_ProducesExpectedManufacturingResults()
    {
        var samplePath = Path.Combine(AppContext.BaseDirectory, "demo-batch.json");
        var sample = await File.ReadAllTextAsync(samplePath);
        using var document = JsonDocument.Parse(sample);
        var expectedCount = document.RootElement.GetProperty("records").GetArrayLength();

        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var content = new StringContent(sample, Encoding.UTF8, "application/json");
        var firstResponse = await client.PostAsync("/api/import/records", content);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        var firstImport = await firstResponse.Content.ReadFromJsonAsync<ImportBatchResult>();
        Assert.NotNull(firstImport);
        Assert.Equal(expectedCount, firstImport.ImportedCount + firstImport.SkippedCount);

        using var replayContent = new StringContent(sample, Encoding.UTF8, "application/json");
        var replayResponse = await client.PostAsync("/api/import/records", replayContent);
        Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);

        var replay = await replayResponse.Content.ReadFromJsonAsync<ImportBatchResult>();
        Assert.NotNull(replay);
        Assert.Equal(0, replay.ImportedCount);
        Assert.Equal(expectedCount, replay.SkippedCount);

        var asOf = Uri.EscapeDataString("2026-11-01T12:00:00+05:00");
        var periodStart = Uri.EscapeDataString("2026-11-01T08:00:00+05:00");

        var overdue = await client.GetFromJsonAsync<List<OverdueProductionTask>>(
            $"/api/production/overdue-tasks?asOf={asOf}");

        Assert.NotNull(overdue);
        var task = Assert.Single(overdue.Where(item => item.TaskId == "J-01"));
        Assert.Equal(60m, task.AcceptedGoodOutput);
        Assert.Equal(40m, task.RemainingQuantity);
        Assert.Equal(120m, task.OverdueMinutes);
        Assert.DoesNotContain(overdue, item => item.TaskId == "J-02");

        var downtime = await client.GetFromJsonAsync<EquipmentDowntimeReport>(
            $"/api/equipment/EQ-01/downtime?periodStart={periodStart}&periodEnd={asOf}");

        Assert.NotNull(downtime);
        Assert.Equal(2, downtime.EventCount);
        Assert.Equal(60m, downtime.DowntimeMinutes);
        Assert.Equal(60m, downtime.UnplannedDowntimeMinutes);
        Assert.Equal(0m, downtime.PlannedDowntimeMinutes);
        Assert.Equal(75m, downtime.AvailabilityPercent);

        var variances = await client.GetFromJsonAsync<List<MaterialConsumptionVariance>>(
            $"/api/materials/consumption-variance?asOf={asOf}");

        Assert.NotNull(variances);
        var material = Assert.Single(variances.Where(item => item.TaskId == "J-02"));
        Assert.Equal(50m, material.AcceptedOutput);
        Assert.Equal(100m, material.ExpectedConsumption);
        Assert.Equal(115m, material.ConsumedQuantity);
        Assert.Equal(5m, material.ReturnedQuantity);
        Assert.Equal(110m, material.NetConsumption);
        Assert.Equal(10m, material.VarianceQuantity);
        Assert.Equal(10m, material.VariancePercent);

        await using var context = database.CreateContext();
        var sourceRecords = await context.SourceRecords.CountAsync(item => item.SourceSystem == "prodexa-demo");
        Assert.Equal(expectedCount, sourceRecords);
    }

    private WebApplicationFactory<Program> CreateFactory()
    {
        var connectionString = Environment.GetEnvironmentVariable("PRODEXA_TEST_CONNECTION_STRING");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("PRODEXA_TEST_CONNECTION_STRING must point to PostgreSQL.");
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
}
