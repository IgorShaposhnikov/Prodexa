using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Prodexa.Core.Importing;
using Prodexa.Importing.Models;
using Prodexa.Persistence;

namespace Prodexa.Importing.Services;

/// <summary>
/// Saves reusable mapping profiles and converts CSV files through the existing transactional importer.
/// </summary>
public sealed class CsvMappingService(
    ProdexaDbContext context,
    CsvTargetCatalog catalog,
    CsvMappingEngine engine,
    ManufacturingImportService importer)
{
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Stores a validated CSV mapping profile in PostgreSQL.
    /// </summary>
    public async Task<CsvMappingDefinition> CreateAsync(CsvMappingDefinition definition, CancellationToken cancellationToken)
    {
        catalog.Validate(definition);

        var profile = new CsvMappingProfile
        {
            Id = definition.Id,
            Name = definition.Name,
            SourceSystem = definition.SourceSystem,
            Target = definition.Target,
            DefinitionJson = JsonSerializer.Serialize(definition, _jsonOptions),
            CreatedAt = DateTimeOffset.UtcNow
        };

        context.CsvMappingProfiles.Add(profile);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgres
            && postgres.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new ImportConflictException($"CSV mapping profile '{definition.Id}' already exists.");
        }

        return definition;
    }

    /// <summary>
    /// Returns the saved JSON definition, or null for an unknown profile.
    /// </summary>
    public async Task<CsvMappingDefinition?> GetAsync(string id, CancellationToken cancellationToken)
    {
        var profile = await context.CsvMappingProfiles.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        return profile is null ? null : JsonSerializer.Deserialize<CsvMappingDefinition>(profile.DefinitionJson, _jsonOptions);
    }

    /// <summary>
    /// Lists previously saved mapping definitions for a client-side mapping editor.
    /// </summary>
    public async Task<IReadOnlyList<CsvMappingDefinition>> ListAsync(CancellationToken cancellationToken)
    {
        var profiles = await context.CsvMappingProfiles.AsNoTracking()
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);

        return profiles.Select(item => JsonSerializer.Deserialize<CsvMappingDefinition>(item.DefinitionJson, _jsonOptions)!)
            .ToList();
    }

    /// <summary>
    /// Validates and converts a small client-selected sample without database writes.
    /// </summary>
    public CsvMappingPreviewResult Preview(
        CsvMappingDefinition definition,
        string datasetId,
        IReadOnlyList<Dictionary<string, string>> rows)
    {
        ValidateDataset(datasetId);
        catalog.Validate(definition);

        if (rows is null || rows.Count is < 1 or > 20)
        {
            throw new ImportValidationException("A mapping preview requires between 1 and 20 CSV rows.");
        }

        var records = new List<ImportRecordRequest>();
        var filtered = 0;

        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];

            if (row is null || row.Count is < 1 or > 100
                || row.Any(item => item.Value is null || item.Value.Length > 8192))
            {
                throw new ImportValidationException($"Invalid CSV sample row {index + 1}.");
            }

            engine.ValidateColumns(definition, row.Keys.ToArray());
            var record = engine.MapRow(definition, row, datasetId, index + 1);

            if (record is null)
            {
                filtered++;
                continue;
            }

            records.Add(record);
        }

        return new CsvMappingPreviewResult
        {
            RowsScanned = rows.Count,
            FilteredRows = filtered,
            Records = records
        };
    }

    /// <summary>
    /// Processes a large CSV in independently committed batches, allowing safe idempotent replay.
    /// </summary>
    public async Task<CsvMappingImportResult> ImportAsync(
        CsvMappingDefinition definition,
        string datasetId,
        string csvPath,
        CancellationToken cancellationToken)
    {
        ValidateDataset(datasetId);
        catalog.Validate(definition);

        using var reader = new CsvFileReader(csvPath);
        engine.ValidateColumns(definition, reader.Columns);

        var records = new List<ImportRecordRequest>(500);
        var scanned = 0;
        var filtered = 0;
        var imported = 0;
        var skipped = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var row = reader.ReadRow();

            if (row is null)
            {
                break;
            }

            scanned++;
            var record = engine.MapRow(definition, row, datasetId, scanned);

            if (record is null)
            {
                filtered++;
                continue;
            }

            records.Add(record);

            if (records.Count == 500)
            {
                var result = await importer.ImportAsync(new ImportBatchRequest
                {
                    SourceSystem = definition.SourceSystem,
                    Records = records
                }, cancellationToken);

                imported += result.ImportedCount;
                skipped += result.SkippedCount;
                records = new List<ImportRecordRequest>(500);
            }
        }

        if (records.Count > 0)
        {
            var result = await importer.ImportAsync(new ImportBatchRequest
            {
                SourceSystem = definition.SourceSystem,
                Records = records
            }, cancellationToken);

            imported += result.ImportedCount;
            skipped += result.SkippedCount;
        }

        return new CsvMappingImportResult
        {
            RowsScanned = scanned,
            FilteredRows = filtered,
            ImportedCount = imported,
            SkippedCount = skipped
        };
    }

    private void ValidateDataset(string datasetId)
    {
        if (string.IsNullOrWhiteSpace(datasetId) || datasetId.Length > 100)
        {
            throw new ImportValidationException("datasetId must contain between 1 and 100 characters.");
        }
    }
}
