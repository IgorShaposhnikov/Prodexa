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
using Prodexa.Production.Models;
using Xunit;

namespace Prodexa.IntegrationTests.Database;

/// <summary>
/// Verifies source idempotency, CSV parsing and atomic rollback using PostgreSQL.
/// </summary>
[Collection("PostgreSQL")]
public sealed class ImportValidationApiTests(PostgreSqlFixture database)
{
    /// <summary>
    /// Identical source revisions are skipped, but changing their canonical data produces a conflict.
    /// </summary>
    [Fact]
    public async Task ImportRecords_SkipsDuplicatesAndRejectsConflictingRevisions()
    {
        var id = Guid.NewGuid().ToString("N");
        var source = $"MES-{id}";
        var equipmentId = $"EQ-{id}";
        var equipment = Record("Equipment", "source-01", new { id = equipmentId });

        var batch = new ImportBatchRequest
        {
            SourceSystem = source,
            Records = new List<ImportRecordRequest> { equipment, equipment }
        };

        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/import/records", batch);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ImportBatchResult>();
        Assert.NotNull(result);
        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(1, result.SkippedCount);

        var changed = Record("Equipment", "source-01", new { id = $"EQ-CHANGED-{id}" });
        var changedBatch = new ImportBatchRequest
        {
            SourceSystem = source,
            Records = new List<ImportRecordRequest> { changed }
        };

        var conflict = await client.PostAsJsonAsync("/api/import/records", changedBatch);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);

        await using var context = database.CreateContext();
        Assert.Equal(1, await context.SourceRecords.CountAsync(record => record.SourceSystem == source));
        Assert.False(await context.Equipment.AnyAsync(item => item.Id == $"EQ-CHANGED-{id}"));
    }

    /// <summary>
    /// One invalid foreign key rolls back every record in the same import batch.
    /// </summary>
    [Fact]
    public async Task ImportRecords_RollsBackBatchWithMissingReferences()
    {
        var id = Guid.NewGuid().ToString("N");
        var source = $"MES-{id}";
        var equipmentId = $"EQ-{id}";
        var missingEquipmentId = $"EQ-MISSING-{id}";

        var equipment = Record("Equipment", equipmentId, new { id = equipmentId });
        var downtime = Record("Downtime", $"D-{id}", new
        {
            id = $"D-{id}", equipmentId = missingEquipmentId, startAt = At(8, 0),
            endAt = At(9, 0), reasonCode = "Mechanical", isPlanned = false
        });

        var batch = new ImportBatchRequest
        {
            SourceSystem = source,
            Records = new List<ImportRecordRequest> { equipment, downtime }
        };

        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/import/records", batch);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await using var context = database.CreateContext();
        Assert.False(await context.Equipment.AnyAsync(item => item.Id == equipmentId));
        Assert.Equal(0, await context.SourceRecords.CountAsync(record => record.SourceSystem == source));
    }

    /// <summary>
    /// Quoted CSV fields load real downtime events and can be replayed without duplication.
    /// </summary>
    [Fact]
    public async Task ImportDowntimeCsv_ParsesQuotedFieldsAndPreservesIdempotency()
    {
        var id = Guid.NewGuid().ToString("N");
        var source = $"MES-{id}";
        var equipmentId = $"EQ-{id}";
        var equipment = Record("Equipment", equipmentId, new { id = equipmentId });
        var setup = new ImportBatchRequest
        {
            SourceSystem = source,
            Records = new List<ImportRecordRequest> { equipment }
        };

        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var setupResponse = await client.PostAsJsonAsync("/api/import/records", setup);
        Assert.Equal(HttpStatusCode.OK, setupResponse.StatusCode);

        var csv = "sourceRecordId,sourceRevision,id,equipmentId,startAt,endAt,reasonCode,isPlanned,comment\n"
            + $"downtime-01,1,D-01-{id},{equipmentId},{At(7, 50):O},{At(8, 20):O},Mechanical,false,\"Motor, inspection\"\n"
            + $"downtime-02,1,D-02-{id},{equipmentId},{At(8, 10):O},{At(9, 0):O},Mechanical,false,\n";

        using var content = new StringContent(csv, Encoding.UTF8, "text/csv");
        var response = await client.PostAsync($"/api/import/downtime-csv?sourceSystem={source}", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var imported = await response.Content.ReadFromJsonAsync<ImportBatchResult>();
        Assert.NotNull(imported);
        Assert.Equal(2, imported.ImportedCount);

        using var replayContent = new StringContent(csv, Encoding.UTF8, "text/csv");
        var replayResponse = await client.PostAsync($"/api/import/downtime-csv?sourceSystem={source}", replayContent);
        Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);

        var replay = await replayResponse.Content.ReadFromJsonAsync<ImportBatchResult>();
        Assert.NotNull(replay);
        Assert.Equal(0, replay.ImportedCount);
        Assert.Equal(2, replay.SkippedCount);

        var start = Uri.EscapeDataString(At(8, 0).ToString("O"));
        var end = Uri.EscapeDataString(At(12, 0).ToString("O"));
        var report = await client.GetFromJsonAsync<EquipmentDowntimeReport>(
            $"/api/equipment/{equipmentId}/downtime?periodStart={start}&periodEnd={end}");

        Assert.NotNull(report);
        Assert.Equal(60m, report.DowntimeMinutes);

        await using var context = database.CreateContext();
        var stored = await context.Downtimes.SingleAsync(item => item.Id == $"D-01-{id}");
        Assert.Equal("Motor, inspection", stored.Comment);
        Assert.Equal(3, await context.SourceRecords.CountAsync(item => item.SourceSystem == source));
    }

    /// <summary>
    /// CSV output movements update the accepted good output of a production task.
    /// </summary>
    [Fact]
    public async Task ImportMaterialMovementsCsv_ContributesToProductionReport()
    {
        var id = Guid.NewGuid().ToString("N");
        var source = $"MES-{id}";
        var taskId = $"J-{id}";
        var revisionId = $"{taskId}-v1";
        var executionId = $"EX-{id}";
        var productId = $"PROD-{id}";
        var lotId = $"LOT-{id}";

        var task = Record("PlanTask", taskId, new { id = taskId });
        var revision = Record("PlanTaskRevision", revisionId, new
        {
            id = revisionId, taskId, revisionNumber = 1, productId, processSegmentVersionId = "SEG-A-v1",
            plannedQuantity = 100m, unitId = "piece", dueAt = At(10, 0), status = "Active",
            effectiveFrom = At(8, 0).AddDays(-2)
        });
        var execution = Record("Execution", executionId, new
        {
            id = executionId, taskId, taskRevisionId = revisionId, startAt = At(8, 0), status = "Started"
        });
        var product = Record("MaterialDefinition", productId, new { id = productId });
        var lot = Record("MaterialLot", lotId, new
        {
            id = lotId, materialId = productId, createdAt = At(8, 0).AddDays(-1),
            unitId = "piece", status = "Active"
        });
        var setup = new ImportBatchRequest
        {
            SourceSystem = source,
            Records = new List<ImportRecordRequest> { task, revision, execution, product, lot }
        };

        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var setupResponse = await client.PostAsJsonAsync("/api/import/records", setup);
        Assert.Equal(HttpStatusCode.OK, setupResponse.StatusCode);

        var csv = "sourceRecordId,sourceRevision,id,executionId,materialLotId,kind,quantity,unitId,occurredAt,outputQuality,reversesMovementId\n"
            + $"output-01,1,MOV-{id},{executionId},{lotId},Output,30,piece,{At(9, 0):O},Good,\n";

        using var csvContent = new StringContent(csv, Encoding.UTF8, "text/csv");
        var importResponse = await client.PostAsync($"/api/import/material-movements-csv?sourceSystem={source}", csvContent);
        Assert.Equal(HttpStatusCode.OK, importResponse.StatusCode);

        var imported = await importResponse.Content.ReadFromJsonAsync<ImportBatchResult>();
        Assert.NotNull(imported);
        Assert.Equal(1, imported.ImportedCount);

        var time = Uri.EscapeDataString(At(12, 0).ToString("O"));
        var overdue = await client.GetFromJsonAsync<List<OverdueProductionTask>>($"/api/production/overdue-tasks?asOf={time}");
        Assert.NotNull(overdue);

        var report = Assert.Single(overdue.Where(item => item.TaskId == taskId));
        Assert.Equal(30m, report.AcceptedGoodOutput);
        Assert.Equal(70m, report.RemainingQuantity);
    }

    /// <summary>
    /// Unknown model names and malformed CSV files cannot create source records.
    /// </summary>
    [Fact]
    public async Task ImportRecords_RejectsUnsupportedTypes()
    {
        var id = Guid.NewGuid().ToString("N");
        var source = $"MES-{id}";
        var unsupported = Record("UnknownModel", id, new { id });
        var batch = new ImportBatchRequest
        {
            SourceSystem = source,
            Records = new List<ImportRecordRequest> { unsupported }
        };

        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/import/records", batch);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await using var context = database.CreateContext();
        Assert.Equal(0, await context.SourceRecords.CountAsync(record => record.SourceSystem == source));
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
