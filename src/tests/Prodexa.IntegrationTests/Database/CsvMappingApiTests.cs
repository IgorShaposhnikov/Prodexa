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
using Prodexa.Persistence;
using Xunit;

namespace Prodexa.IntegrationTests.Database;

/// <summary>
/// Verifies configurable CSV mappings through public HTTP endpoints and PostgreSQL.
/// </summary>
[Collection("PostgreSQL")]
public sealed class CsvMappingApiTests(PostgreSqlFixture database)
{
    /// <summary>
    /// Previews an unsaved mapping with client-supplied rows, saves the profile and imports real CSV events.
    /// </summary>
    [Fact]
    public async Task CsvMapping_ConvertsRealisticMachineEventsAndSafelyReplaysTheImport()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var machineId = $"EQ-{suffix}";
        var sourceSystem = $"CSV-{suffix}";
        var profileId = $"PIADE-{suffix}";
        var datasetId = "packaging-2020";
        var csv = "equipment_ID,interval_start,end,alarm,type\n"
            + $"{machineId},2020-01-01 10:00:00,1577876400,MAINTENANCE,scheduled_downtime\n"
            + $"{machineId},2020-01-01 11:00:00,1577878200,NONE,production\n"
            + $"{machineId},2020-01-01 10:30:00,1577878200,MECHANICAL,downtime\n";

        var profile = CreateDowntimeMapping(profileId, sourceSystem);

        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var targetResponse = await client.GetFromJsonAsync<List<CsvTargetDefinition>>("/api/import/targets");
        Assert.NotNull(targetResponse);

        var downtimeTarget = Assert.Single(targetResponse, target => target.Target == "Downtime");
        Assert.Contains(downtimeTarget.Fields, field => field.Name == "equipmentId" && field.Required);
        Assert.Contains(downtimeTarget.Fields, field => field.Name == "startAt" && field.ConvertTo == "datetime_utc");

        var previewRequest = new CsvMappingPreviewRequest
        {
            Mapping = profile,
            DatasetId = datasetId,
            Rows = new List<Dictionary<string, string>>
            {
                new()
                {
                    ["equipment_ID"] = machineId,
                    ["interval_start"] = "2020-01-01 10:00:00",
                    ["end"] = "1577876400",
                    ["alarm"] = "MAINTENANCE",
                    ["type"] = "scheduled_downtime"
                },
                new()
                {
                    ["equipment_ID"] = machineId,
                    ["interval_start"] = "2020-01-01 11:00:00",
                    ["end"] = "1577878200",
                    ["alarm"] = "NONE",
                    ["type"] = "production"
                },
                new()
                {
                    ["equipment_ID"] = machineId,
                    ["interval_start"] = "2020-01-01 10:30:00",
                    ["end"] = "1577878200",
                    ["alarm"] = "MECHANICAL",
                    ["type"] = "downtime"
                }
            }
        };

        // The client can preview its mapping before saving anything in PostgreSQL.
        var previewResponse = await client.PostAsJsonAsync("/api/import/mappings/preview", previewRequest);
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);

        var preview = await previewResponse.Content.ReadFromJsonAsync<CsvMappingPreviewResult>();
        Assert.NotNull(preview);
        Assert.Equal(3, preview.RowsScanned);
        Assert.Equal(1, preview.FilteredRows);
        Assert.Equal(2, preview.Records.Count);
        Assert.True(preview.Records[0].Data.GetProperty("isPlanned").GetBoolean());
        Assert.False(preview.Records[1].Data.GetProperty("isPlanned").GetBoolean());
        Assert.Equal(
            new DateTimeOffset(2020, 1, 1, 10, 0, 0, TimeSpan.Zero),
            DateTimeOffset.Parse(preview.Records[0].Data.GetProperty("startAt").GetString()!));
        Assert.StartsWith("csv-", preview.Records[0].Data.GetProperty("id").GetString());

        await using (var previewContext = database.CreateContext())
        {
            Assert.False(await previewContext.CsvMappingProfiles.AnyAsync(item => item.Id == profileId));
        }

        var createResponse = await client.PostAsJsonAsync("/api/import/mappings", profile);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var stored = await client.GetFromJsonAsync<CsvMappingDefinition>($"/api/import/mappings/{profileId}");
        Assert.NotNull(stored);
        Assert.Equal(profileId, stored.Id);
        Assert.Equal("interval_start", stored.Fields["startAt"].Column);
        Assert.Equal("datetime_utc", stored.Fields["startAt"].ConvertTo);
        Assert.Equal("scheduled_downtime", stored.Fields["isPlanned"].Equals);

        var equipmentBatch = new ImportBatchRequest
        {
            SourceSystem = sourceSystem,
            Records = new List<ImportRecordRequest>
            {
                new()
                {
                    RecordType = "Equipment",
                    SourceRecordId = machineId,
                    SourceRevision = "1",
                    Data = JsonSerializer.SerializeToElement(new { id = machineId })
                }
            }
        };

        var equipmentResponse = await client.PostAsJsonAsync("/api/import/records", equipmentBatch);
        Assert.Equal(HttpStatusCode.OK, equipmentResponse.StatusCode);

        using (var importContent = CsvFile(csv))
        {
            var importResponse = await client.PostAsync(
                $"/api/import/mappings/{profileId}/import?datasetId={datasetId}", importContent);
            Assert.Equal(HttpStatusCode.OK, importResponse.StatusCode);

            var result = await importResponse.Content.ReadFromJsonAsync<CsvMappingImportResult>();
            Assert.NotNull(result);
            Assert.Equal(3, result.RowsScanned);
            Assert.Equal(1, result.FilteredRows);
            Assert.Equal(2, result.ImportedCount);
            Assert.Equal(0, result.SkippedCount);
        }

        using (var replayContent = CsvFile(csv))
        {
            var replayResponse = await client.PostAsync(
                $"/api/import/mappings/{profileId}/import?datasetId={datasetId}", replayContent);
            Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);

            var replay = await replayResponse.Content.ReadFromJsonAsync<CsvMappingImportResult>();
            Assert.NotNull(replay);
            Assert.Equal(0, replay.ImportedCount);
            Assert.Equal(2, replay.SkippedCount);
        }

        var start = Uri.EscapeDataString("2020-01-01T10:00:00Z");
        var end = Uri.EscapeDataString("2020-01-01T12:00:00Z");
        var report = await client.GetFromJsonAsync<EquipmentDowntimeReport>(
            $"/api/equipment/{machineId}/downtime?periodStart={start}&periodEnd={end}");

        Assert.NotNull(report);
        Assert.Equal(2, report.EventCount);
        Assert.Equal(90m, report.DowntimeMinutes);
        Assert.Equal(60m, report.PlannedDowntimeMinutes);
        Assert.Equal(60m, report.UnplannedDowntimeMinutes);
        Assert.Equal(25m, report.AvailabilityPercent);

        await using var context = database.CreateContext();
        Assert.True(await context.CsvMappingProfiles.AnyAsync(item => item.Id == profileId));
        Assert.Equal(3, await context.SourceRecords.CountAsync(record => record.SourceSystem == sourceSystem));
    }

    /// <summary>
    /// The browser workspace and its ES modules are served by the API.
    /// </summary>
    [Fact]
    public async Task CsvWorkspace_ServesTheImportPageAndScripts()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var page = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("CSV import", await page.Content.ReadAsStringAsync());

        foreach (var path in new[] { "/import.js", "/csv-sample.mjs", "/styles.css" })
        {
            var asset = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, asset.StatusCode);
            Assert.NotEmpty(await asset.Content.ReadAsStringAsync());

            if (path.EndsWith(".js") || path.EndsWith(".mjs"))
            {
                Assert.Contains("javascript", asset.Content.Headers.ContentType!.MediaType!);
            }
        }
    }

    /// <summary>
    /// Fractional Unix timestamps retain the precision used by manufacturing datasets.
    /// </summary>
    [Theory]
    [InlineData("unix_seconds", "1577877751.957", "2020-01-01T11:22:31.957+00:00")]
    [InlineData("unix_milliseconds", "1577877751957", "2020-01-01T11:22:31.957+00:00")]
    [InlineData("unix_seconds", "-0.125", "1969-12-31T23:59:59.875+00:00")]
    [InlineData("unix_milliseconds", "0.125", "1970-01-01T00:00:00.000125+00:00")]
    public async Task CsvMappingPreview_PreservesFractionalUnixTimestamps(string format, string value, string expected)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var mapping = CreateDowntimeMapping($"PROFILE-{suffix}", $"SOURCE-{suffix}");
        mapping.Fields["endAt"] = new CsvFieldMapping
        {
            Column = "end",
            ConvertTo = "datetime_utc",
            SourceFormat = format
        };

        var request = new CsvMappingPreviewRequest
        {
            Mapping = mapping,
            DatasetId = "fractional-epoch",
            Rows = new List<Dictionary<string, string>>
            {
                new()
                {
                    ["equipment_ID"] = "EQ-01",
                    ["interval_start"] = "1969-12-31 00:00:00",
                    ["end"] = value,
                    ["alarm"] = "A_000",
                    ["type"] = "downtime"
                }
            }
        };

        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/import/mappings/preview", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<CsvMappingPreviewResult>();
        Assert.NotNull(result);
        var record = Assert.Single(result.Records);
        Assert.Equal(DateTimeOffset.Parse(expected), record.Data.GetProperty("endAt").GetDateTimeOffset());
    }

    /// <summary>
    /// OpenAPI exposes the mapping contract and Swagger UI is available during development.
    /// </summary>
    [Fact]
    public async Task CsvMapping_AppearsInDevelopmentOpenApiAndSwaggerUi()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var schemaResponse = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, schemaResponse.StatusCode);

        var schema = await schemaResponse.Content.ReadAsStringAsync();
        Assert.Contains("/api/import/mappings", schema);
        Assert.Contains("/api/import/mappings/preview", schema);
        Assert.DoesNotContain("/api/import/csv/inspect", schema);
        Assert.Contains("convertTo", schema);

        var uiResponse = await client.GetAsync("/swagger/index.html");
        Assert.Equal(HttpStatusCode.OK, uiResponse.StatusCode);
    }

    /// <summary>
    /// Imports more than 500 CSV rows while deduplicating repeated source keys across batch boundaries.
    /// </summary>
    [Fact]
    public async Task CsvMapping_ImportsMultipleBatchesWithoutDuplicateEquipment()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var equipmentId = $"EQ-BATCH-{suffix}";
        var profileId = $"PROFILE-BATCH-{suffix}";
        var source = $"SOURCE-BATCH-{suffix}";

        var mapping = new CsvMappingDefinition
        {
            Id = profileId,
            Name = "Equipment from repeated source rows",
            SourceSystem = source,
            SourceRecordIdColumn = "machine_id",
            Target = "Equipment",
            Fields = new Dictionary<string, CsvFieldMapping>
            {
                ["id"] = new() { Column = "machine_id" }
            }
        };

        var csv = new StringBuilder("machine_id\n");

        for (var index = 0; index < 501; index++)
        {
            csv.AppendLine(equipmentId);
        }

        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var profileResponse = await client.PostAsJsonAsync("/api/import/mappings", mapping);
        Assert.Equal(HttpStatusCode.Created, profileResponse.StatusCode);

        using (var content = CsvFile(csv.ToString()))
        {
            var response = await client.PostAsync(
                $"/api/import/mappings/{profileId}/import?datasetId=equipment-batch", content);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var result = await response.Content.ReadFromJsonAsync<CsvMappingImportResult>();
            Assert.NotNull(result);
            Assert.Equal(501, result.RowsScanned);
            Assert.Equal(1, result.ImportedCount);
            Assert.Equal(500, result.SkippedCount);
        }

        using (var content = CsvFile(csv.ToString()))
        {
            var response = await client.PostAsync(
                $"/api/import/mappings/{profileId}/import?datasetId=equipment-batch", content);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var replay = await response.Content.ReadFromJsonAsync<CsvMappingImportResult>();
            Assert.NotNull(replay);
            Assert.Equal(0, replay.ImportedCount);
            Assert.Equal(501, replay.SkippedCount);
        }

        await using var context = database.CreateContext();
        Assert.Equal(1, await context.Equipment.CountAsync(item => item.Id == equipmentId));
        Assert.Equal(1, await context.SourceRecords.CountAsync(item => item.SourceSystem == source));
    }

    /// <summary>
    /// Validates client-parsed preview rows and rejects oversized samples.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public async Task CsvMappingPreview_RejectsInvalidSampleSizes(int rowCount)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var mapping = CreateDowntimeMapping($"PROFILE-{suffix}", $"SOURCE-{suffix}");
        var sampleRow = new Dictionary<string, string>
        {
            ["equipment_ID"] = "EQ-01",
            ["interval_start"] = "2020-01-01 10:00:00",
            ["end"] = "1577876400",
            ["alarm"] = "MECHANICAL",
            ["type"] = "downtime"
        };

        var request = new CsvMappingPreviewRequest
        {
            Mapping = mapping,
            DatasetId = "sample-01",
            Rows = Enumerable.Range(0, rowCount).Select(_ => new Dictionary<string, string>(sampleRow)).ToList()
        };

        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/import/mappings/preview", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Rejects a preview with missing source columns without persisting a mapping.
    /// </summary>
    [Fact]
    public async Task CsvMappingPreview_RejectsMissingMappedColumns()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var mapping = CreateDowntimeMapping($"PROFILE-{suffix}", $"SOURCE-{suffix}");
        var request = new CsvMappingPreviewRequest
        {
            Mapping = mapping,
            DatasetId = "sample-02",
            Rows = new List<Dictionary<string, string>>
            {
                new() { ["equipment_ID"] = "EQ-01", ["type"] = "downtime" }
            }
        };

        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/import/mappings/preview", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Missing columns and unknown target properties are rejected before any data is persisted.
    /// </summary>
    [Fact]
    public async Task CsvMapping_RejectsUnknownFieldsAndMissingColumns()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var id = $"BAD-MAP-{suffix}";
        var source = $"BAD-SOURCE-{suffix}";
        var profile = CreateDowntimeMapping(id, source);

        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var invalid = new CsvMappingDefinition
        {
            Id = $"INVALID-{suffix}",
            Name = "Incorrect target property",
            SourceSystem = source,
            Target = "Downtime",
            Fields = new Dictionary<string, CsvFieldMapping>
            {
                ["equipmentId"] = new() { Column = "equipment_ID" },
                ["invalidProperty"] = new() { Column = "random" }
            }
        };

        var invalidResponse = await client.PostAsJsonAsync("/api/import/mappings", invalid);
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);

        var created = await client.PostAsJsonAsync("/api/import/mappings", profile);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var malformed = "equipment_ID,interval_start,alarm,type\n"
            + $"EQ-{suffix},2020-01-01 10:00:00,ERROR,downtime\n";

        using var content = CsvFile(malformed);
        var response = await client.PostAsync(
            $"/api/import/mappings/{id}/import?datasetId=missing-column", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await using var context = database.CreateContext();
        Assert.Equal(0, await context.SourceRecords.CountAsync(record => record.SourceSystem == source));
    }

    /// <summary>
    /// The original source row ID and mapping rules are stable across file replay.
    /// </summary>
    [Fact]
    public async Task CsvMapping_RejectsChangedSourceRowWithSameDatasetKey()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var equipmentId = $"EQ-{suffix}";
        var profileId = $"CONFLICT-{suffix}";
        var source = $"CSV-CONFLICT-{suffix}";
        var profile = CreateDowntimeMapping(profileId, source);

        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var equipment = new ImportBatchRequest
        {
            SourceSystem = source,
            Records = new List<ImportRecordRequest>
            {
                new()
                {
                    RecordType = "Equipment",
                    SourceRecordId = equipmentId,
                    SourceRevision = "1",
                    Data = JsonSerializer.SerializeToElement(new { id = equipmentId })
                }
            }
        };

        var equipmentResponse = await client.PostAsJsonAsync("/api/import/records", equipment);
        Assert.Equal(HttpStatusCode.OK, equipmentResponse.StatusCode);

        var created = await client.PostAsJsonAsync("/api/import/mappings", profile);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var original = "equipment_ID,interval_start,end,alarm,type\n"
            + $"{equipmentId},2020-01-01 10:00:00,1577876400,MECHANICAL,downtime\n";
        var changed = "equipment_ID,interval_start,end,alarm,type\n"
            + $"{equipmentId},2020-01-01 10:00:00,1577876400,ELECTRICAL,downtime\n";

        using (var originalContent = CsvFile(original))
        {
            var initial = await client.PostAsync(
                $"/api/import/mappings/{profileId}/import?datasetId=fixed-file", originalContent);
            Assert.Equal(HttpStatusCode.OK, initial.StatusCode);
        }

        using (var changedContent = CsvFile(changed))
        {
            var response = await client.PostAsync(
                $"/api/import/mappings/{profileId}/import?datasetId=fixed-file", changedContent);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        await using var context = database.CreateContext();
        Assert.Equal(1, await context.Downtimes.CountAsync(item => item.EquipmentId == equipmentId));
    }

    private MultipartFormDataContent CsvFile(string csv)
    {
        var multipart = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        multipart.Add(file, "file", "sample.csv");
        return multipart;
    }

    private CsvMappingDefinition CreateDowntimeMapping(string id, string sourceSystem)
    {
        return new CsvMappingDefinition
        {
            Id = id,
            Name = "Generic machine downtime mapping",
            SourceSystem = sourceSystem,
            Target = "Downtime",
            Filters = new Dictionary<string, List<string>>
            {
                ["type"] = new List<string> { "downtime", "scheduled_downtime" }
            },
            Fields = new Dictionary<string, CsvFieldMapping>
            {
                ["id"] = new() { Generate = "stable_id" },
                ["equipmentId"] = new() { Column = "equipment_ID" },
                ["startAt"] = new()
                {
                    Column = "interval_start",
                    ConvertTo = "datetime_utc",
                    SourceFormat = "utc_naive"
                },
                ["endAt"] = new()
                {
                    Column = "end",
                    ConvertTo = "datetime_utc",
                    SourceFormat = "unix_seconds"
                },
                ["reasonCode"] = new() { Column = "alarm" },
                ["isPlanned"] = new()
                {
                    Column = "type",
                    Equals = "scheduled_downtime",
                    ConvertTo = "boolean"
                }
            }
        };
    }

    private WebApplicationFactory<Program> CreateFactory()
    {
        var connectionString = Environment.GetEnvironmentVariable("PRODEXA_TEST_CONNECTION_STRING");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("PRODEXA_TEST_CONNECTION_STRING must point to a dedicated PostgreSQL database.");
        }

        return new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            host.UseEnvironment("Development");
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
