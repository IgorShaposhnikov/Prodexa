using System.Net;
using System.Net.Http.Json;
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
/// Verifies that source records imported over HTTP feed all three analytical reports.
/// </summary>
[Collection("PostgreSQL")]
public sealed class ManufacturingImportFlowTests(PostgreSqlFixture database)
{
    /// <summary>
    /// A single atomic JSON batch provides data for overdue production, downtime, and material variance.
    /// </summary>
    [Fact]
    public async Task ImportRecords_FeedsAllThreeAnalyticalScenariosWithoutDuplicatingData()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var source = $"MES-{suffix}";
        var asOf = At(12, 0);
        var task01Id = $"J-01-{suffix}";
        var task02Id = $"J-02-{suffix}";
        var revision01Id = $"{task01Id}-v1";
        var revision02Id = $"{task02Id}-v1";
        var execution01Id = $"EX-01-{suffix}";
        var execution02Id = $"EX-02-{suffix}";
        var equipmentId = $"EQ-01-{suffix}";
        var productId = $"PROD-A-{suffix}";
        var materialId = $"MAT-A-{suffix}";
        var productVersionId = $"{productId}-v1";
        var materialVersionId = $"{materialId}-v1";
        var productLotId = $"LOT-PROD-{suffix}";
        var materialLotId = $"LOT-MAT-{suffix}";
        var normId = $"N-01-v1-{suffix}";
        var consumeId = $"MOV-CONSUME-{suffix}";

        var task01 = Record("PlanTask", task01Id, new { id = task01Id });
        var task02 = Record("PlanTask", task02Id, new { id = task02Id });

        var revision01 = Record("PlanTaskRevision", revision01Id, new
        {
            id = revision01Id, taskId = task01Id, revisionNumber = 1, productId,
            processSegmentVersionId = "SEG-A-v1", plannedQuantity = 100m,
            unitId = "piece", dueAt = At(10, 0), status = "Active", effectiveFrom = asOf.AddDays(-10)
        });
        var revision02 = Record("PlanTaskRevision", revision02Id, new
        {
            id = revision02Id, taskId = task02Id, revisionNumber = 1, productId,
            processSegmentVersionId = "SEG-A-v1", plannedQuantity = 50m,
            unitId = "piece", dueAt = At(10, 0), status = "Completed", effectiveFrom = asOf.AddDays(-10)
        });
        var execution01 = Record("Execution", execution01Id, new
        {
            id = execution01Id, taskId = task01Id, taskRevisionId = revision01Id, startAt = At(8, 0), status = "Completed"
        });
        var execution02 = Record("Execution", execution02Id, new
        {
            id = execution02Id, taskId = task02Id, taskRevisionId = revision02Id, startAt = At(8, 0), status = "Completed"
        });

        var equipment = Record("Equipment", equipmentId, new { id = equipmentId });
        var downtime01 = Record("Downtime", $"D-01-{suffix}", new
        {
            id = $"D-01-{suffix}", equipmentId, startAt = At(7, 50),
            endAt = At(8, 20), reasonCode = "Mechanical", isPlanned = false
        });
        var downtime02 = Record("Downtime", $"D-02-{suffix}", new
        {
            id = $"D-02-{suffix}", equipmentId, startAt = At(8, 10),
            endAt = At(9, 0), reasonCode = "Mechanical", isPlanned = false
        });

        var product = Record("MaterialDefinition", productId, new { id = productId });
        var material = Record("MaterialDefinition", materialId, new { id = materialId });
        var productVersion = Record("MaterialDefinitionVersion", productVersionId, new
        {
            id = productVersionId, materialId = productId, versionNumber = 1, name = "Product A",
            materialClassId = "Product", baseUnitId = "piece",
            validFrom = asOf.AddDays(-10), sourceRecordId = $"UPSTREAM-P-{suffix}"
        });
        var materialVersion = Record("MaterialDefinitionVersion", materialVersionId, new
        {
            id = materialVersionId, materialId, versionNumber = 1, name = "Material A",
            materialClassId = "RawMaterial", baseUnitId = "kg",
            validFrom = asOf.AddDays(-10), sourceRecordId = $"UPSTREAM-M-{suffix}"
        });
        var productLot = Record("MaterialLot", productLotId, new
        {
            id = productLotId, materialId = productId, createdAt = asOf.AddDays(-3), unitId = "piece", status = "Active"
        });
        var materialLot = Record("MaterialLot", materialLotId, new
        {
            id = materialLotId, materialId, createdAt = asOf.AddDays(-3), unitId = "kg", status = "Active"
        });

        var norm = Record("ConsumptionNorm", normId, new
        {
            id = normId, normId = $"N-01-{suffix}", versionNumber = 1,
            productVersionId, materialVersionId, processSegmentVersionId = "SEG-A-v1",
            quantityPerOutput = 2m, inputUnitId = "kg", outputUnitId = "piece", validFrom = asOf.AddDays(-10)
        });
        var binding = Record("PlanTaskNorm", $"{revision02Id}/{materialId}", new
        {
            taskRevisionId = revision02Id, materialId, normVersionId = normId
        });

        var output01 = Record("MaterialMovement", $"MOV-OUTPUT-01-{suffix}", new
        {
            id = $"MOV-OUTPUT-01-{suffix}", executionId = execution01Id, materialLotId = productLotId,
            kind = "Output", quantity = 60m, unitId = "piece", occurredAt = At(9, 0), outputQuality = "Good"
        });
        var output02 = Record("MaterialMovement", $"MOV-OUTPUT-02-{suffix}", new
        {
            id = $"MOV-OUTPUT-02-{suffix}", executionId = execution02Id, materialLotId = productLotId,
            kind = "Output", quantity = 50m, unitId = "piece", occurredAt = At(9, 0), outputQuality = "Good"
        });
        var consumed = Record("MaterialMovement", consumeId, new
        {
            id = consumeId, executionId = execution02Id, materialLotId, kind = "Consume",
            quantity = 115m, unitId = "kg", occurredAt = At(9, 10)
        });
        var returned = Record("MaterialMovement", $"MOV-RETURN-{suffix}", new
        {
            id = $"MOV-RETURN-{suffix}", executionId = execution02Id, materialLotId, kind = "Return",
            quantity = 5m, unitId = "kg", occurredAt = At(9, 20), reversesMovementId = consumeId
        });

        var batch = new ImportBatchRequest
        {
            SourceSystem = source,
            Records = new List<ImportRecordRequest>
            {
                task01, task02, revision01, revision02, execution01, execution02,
                equipment, downtime01, downtime02, product, material, productVersion,
                materialVersion, productLot, materialLot, norm, binding,
                output01, output02, consumed, returned
            }
        };

        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/import/records", batch);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var imported = await response.Content.ReadFromJsonAsync<ImportBatchResult>();
        Assert.NotNull(imported);
        Assert.Equal(batch.Records.Count, imported.ImportedCount);
        Assert.Equal(0, imported.SkippedCount);

        var replayResponse = await client.PostAsJsonAsync("/api/import/records", batch);
        Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);

        var replay = await replayResponse.Content.ReadFromJsonAsync<ImportBatchResult>();
        Assert.NotNull(replay);
        Assert.Equal(0, replay.ImportedCount);
        Assert.Equal(batch.Records.Count, replay.SkippedCount);

        var time = Uri.EscapeDataString(asOf.ToString("O"));
        var periodStart = Uri.EscapeDataString(At(8, 0).ToString("O"));

        var overdue = await client.GetFromJsonAsync<List<OverdueProductionTask>>($"/api/production/overdue-tasks?asOf={time}");
        Assert.NotNull(overdue);

        var firstTask = Assert.Single(overdue.Where(report => report.TaskId == task01Id));
        Assert.Equal(60m, firstTask.AcceptedGoodOutput);
        Assert.Equal(40m, firstTask.RemainingQuantity);
        Assert.Equal(120m, firstTask.OverdueMinutes);
        Assert.DoesNotContain(overdue, report => report.TaskId == task02Id);

        var downtime = await client.GetFromJsonAsync<EquipmentDowntimeReport>(
            $"/api/equipment/{equipmentId}/downtime?periodStart={periodStart}&periodEnd={time}");
        Assert.NotNull(downtime);
        Assert.Equal(2, downtime.EventCount);
        Assert.Equal(60m, downtime.DowntimeMinutes);
        Assert.Equal(75m, downtime.AvailabilityPercent);

        var materialVariance = await client.GetFromJsonAsync<List<MaterialConsumptionVariance>>(
            $"/api/materials/consumption-variance?asOf={time}");
        Assert.NotNull(materialVariance);

        var secondTask = Assert.Single(materialVariance.Where(report => report.TaskId == task02Id));
        Assert.Equal(50m, secondTask.AcceptedOutput);
        Assert.Equal(100m, secondTask.ExpectedConsumption);
        Assert.Equal(115m, secondTask.ConsumedQuantity);
        Assert.Equal(5m, secondTask.ReturnedQuantity);
        Assert.Equal(110m, secondTask.NetConsumption);
        Assert.Equal(10m, secondTask.VarianceQuantity);
        Assert.Equal(10m, secondTask.VariancePercent);

        await using var context = database.CreateContext();
        Assert.Equal(batch.Records.Count, await context.SourceRecords.CountAsync(record => record.SourceSystem == source));
    }

    private ImportRecordRequest Record(string type, string id, object data)
    {
        return new ImportRecordRequest
        {
            RecordType = type,
            SourceRecordId = id,
            SourceRevision = "1",
            Data = JsonSerializer.SerializeToElement(data)
        };
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

    private DateTimeOffset At(int hour, int minute)
    {
        return new DateTimeOffset(2026, 11, 1, hour, minute, 0, TimeSpan.FromHours(5));
    }
}
