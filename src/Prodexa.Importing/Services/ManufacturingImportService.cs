using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Prodexa.Core.Importing;
using Prodexa.Core.Materials;
using Prodexa.Core.Production;
using Prodexa.Core.Resources;
using Prodexa.Importing.Models;
using Prodexa.Persistence;

namespace Prodexa.Importing.Services;

/// <summary>
/// Imports source revisions and canonical manufacturing records in an atomic PostgreSQL transaction.
/// </summary>
public sealed class ManufacturingImportService(ProdexaDbContext context)
{
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    /// <summary>
    /// Inserts new source revisions, skips identical replays, and rejects conflicting revisions.
    /// </summary>
    public async Task<ImportBatchResult> ImportAsync(ImportBatchRequest batch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);

        ValidateIdentifier(batch.SourceSystem, "sourceSystem");

        if (batch.Records is null || batch.Records.Count is < 1 or > 500)
        {
            throw new ImportValidationException("An import batch must contain between 1 and 500 records.");
        }

        var imported = 0;
        var skipped = 0;

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        foreach (var record in batch.Records)
        {
            if (record is null)
            {
                throw new ImportValidationException("Import records cannot be null.");
            }

            ValidateIdentifier(record.RecordType, "recordType");
            ValidateIdentifier(record.SourceRecordId, "sourceRecordId");
            ValidateIdentifier(record.SourceRevision, "sourceRevision");

            if (record.Data.ValueKind != JsonValueKind.Object)
            {
                throw new ImportValidationException("Each imported data value must be a JSON object.");
            }

            var canonicalJson = JsonSerializer.Serialize(record.Data);
            var payloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson)));

            // FindAsync also checks records already tracked in the current batch.
            var key = new object[] { batch.SourceSystem, record.RecordType, record.SourceRecordId, record.SourceRevision };
            var previous = await context.SourceRecords.FindAsync(key, cancellationToken);

            if (previous is not null)
            {
                if (!string.Equals(previous.PayloadHash, payloadHash, StringComparison.Ordinal))
                {
                    throw new ImportConflictException(
                        $"Source record '{record.SourceRecordId}' changed without a new revision.");
                }

                skipped++;
                continue;
            }

            var targetId = AddRecord(record);

            var source = new SourceRecord
            {
                SourceSystem = batch.SourceSystem,
                RecordType = record.RecordType,
                SourceRecordId = record.SourceRecordId,
                SourceRevision = record.SourceRevision,
                SourceEventAt = record.SourceEventAt,
                ReceivedAt = DateTimeOffset.UtcNow,
                TargetId = targetId,
                PayloadHash = payloadHash
            };

            context.SourceRecords.Add(source);
            imported++;
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgres
            && postgres.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new ImportConflictException("A canonical record or source revision already exists.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgres
            && postgres.SqlState is PostgresErrorCodes.ForeignKeyViolation
                or PostgresErrorCodes.CheckViolation or PostgresErrorCodes.NotNullViolation)
        {
            throw new ImportValidationException("An imported record violates a database constraint or references a missing record.");
        }

        return new ImportBatchResult
        {
            SourceSystem = batch.SourceSystem,
            ImportedCount = imported,
            SkippedCount = skipped
        };
    }

    private string AddRecord(ImportRecordRequest record)
    {
        switch (record.RecordType)
        {
            case nameof(PlanTask):
            {
                var task = Deserialize<PlanTask>(record.Data);
                ValidateIdentifier(task.Id, "data.id");
                context.PlanTasks.Add(task);
                return task.Id;
            }
            case nameof(PlanTaskRevision):
            {
                var revision = Deserialize<PlanTaskRevision>(record.Data);
                ValidateIdentifier(revision.Id, "data.id");
                ValidateIdentifier(revision.TaskId, "data.taskId");

                if (revision.RevisionNumber <= 0 || revision.PlannedQuantity < 0m
                    || (revision.EffectiveTo.HasValue && revision.EffectiveTo <= revision.EffectiveFrom))
                {
                    throw new ImportValidationException("Invalid production task revision number, quantity, or effective period.");
                }

                context.PlanTaskRevisions.Add(revision);
                return revision.Id;
            }
            case nameof(Execution):
            {
                var execution = Deserialize<Execution>(record.Data);
                ValidateIdentifier(execution.Id, "data.id");
                ValidateIdentifier(execution.TaskRevisionId, "data.taskRevisionId");

                if (execution.EndAt < execution.StartAt)
                {
                    throw new ImportValidationException("An execution cannot end before it starts.");
                }

                context.Executions.Add(execution);
                return execution.Id;
            }
            case nameof(Equipment):
            {
                var equipment = Deserialize<Equipment>(record.Data);
                ValidateIdentifier(equipment.Id, "data.id");
                context.Equipment.Add(equipment);
                return equipment.Id;
            }
            case nameof(Downtime):
            {
                var downtime = Deserialize<Downtime>(record.Data);
                ValidateIdentifier(downtime.Id, "data.id");
                ValidateIdentifier(downtime.EquipmentId, "data.equipmentId");

                if (downtime.EndAt < downtime.StartAt)
                {
                    throw new ImportValidationException("Downtime cannot end before it starts.");
                }

                context.Downtimes.Add(downtime);
                return downtime.Id;
            }
            case nameof(MaterialDefinition):
            {
                var material = Deserialize<MaterialDefinition>(record.Data);
                ValidateIdentifier(material.Id, "data.id");
                context.MaterialDefinitions.Add(material);
                return material.Id;
            }
            case nameof(MaterialDefinitionVersion):
            {
                var version = Deserialize<MaterialDefinitionVersion>(record.Data);
                ValidateIdentifier(version.Id, "data.id");
                context.MaterialDefinitionVersions.Add(version);
                return version.Id;
            }
            case nameof(MaterialLot):
            {
                var lot = Deserialize<MaterialLot>(record.Data);
                ValidateIdentifier(lot.Id, "data.id");
                context.MaterialLots.Add(lot);
                return lot.Id;
            }
            case nameof(MaterialMovement):
            {
                var movement = Deserialize<MaterialMovement>(record.Data);
                ValidateIdentifier(movement.Id, "data.id");

                if (movement.Quantity <= 0m
                    || (movement.Kind == MaterialMovementKind.Output) != movement.OutputQuality.HasValue)
                {
                    throw new ImportValidationException("Material movements must have positive quantities and valid output quality.");
                }

                if (movement.Kind == MaterialMovementKind.Return && string.IsNullOrWhiteSpace(movement.ReversesMovementId))
                {
                    throw new ImportValidationException("A material return must identify the consumption event it reverses.");
                }

                context.MaterialMovements.Add(movement);
                return movement.Id;
            }
            case nameof(ConsumptionNorm):
            {
                var norm = Deserialize<ConsumptionNorm>(record.Data);
                ValidateIdentifier(norm.Id, "data.id");

                if (norm.QuantityPerOutput <= 0m)
                {
                    throw new ImportValidationException("Consumption norms must have a positive material rate.");
                }

                context.ConsumptionNorms.Add(norm);
                return norm.Id;
            }
            case nameof(PlanTaskNorm):
            {
                var binding = Deserialize<PlanTaskNorm>(record.Data);
                ValidateIdentifier(binding.TaskRevisionId, "data.taskRevisionId");
                ValidateIdentifier(binding.MaterialId, "data.materialId");
                context.PlanTaskNorms.Add(binding);
                return $"{binding.TaskRevisionId}/{binding.MaterialId}";
            }
            default:
                throw new ImportValidationException($"Unsupported record type '{record.RecordType}'.");
        }
    }

    private T Deserialize<T>(JsonElement data) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(data, _serializerOptions)
                ?? throw new ImportValidationException("The imported data cannot be null.");
        }
        catch (JsonException exception)
        {
            throw new ImportValidationException($"Invalid {typeof(T).Name} record: {exception.Message}");
        }
    }

    private void ValidateIdentifier(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 100)
        {
            throw new ImportValidationException($"'{name}' must contain between 1 and 100 characters.");
        }
    }
}
