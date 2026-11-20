using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.VisualBasic.FileIO;
using Prodexa.Core.Materials;
using Prodexa.Core.Resources;
using Prodexa.Importing.Models;

namespace Prodexa.Importing.Services;

/// <summary>
/// Converts quoted CSV event rows into the same source records accepted by JSON imports.
/// </summary>
public sealed class CsvImportParser
{
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    /// <summary>
    /// Reads equipment downtime events with explicit source revision identifiers.
    /// </summary>
    public ImportBatchRequest ParseDowntimes(string sourceSystem, string csv)
    {
        var headers = new[]
        {
            "sourceRecordId", "sourceRevision", "id", "equipmentId", "startAt",
            "endAt", "reasonCode", "isPlanned", "comment"
        };

        var rows = ReadRows(csv, headers);
        var records = new List<ImportRecordRequest>();

        foreach (var row in rows)
        {
            var start = ParseDate(row[4], "startAt");
            var end = string.IsNullOrWhiteSpace(row[5]) ? (DateTimeOffset?)null : ParseDate(row[5], "endAt");

            if (!bool.TryParse(row[7], out var isPlanned))
            {
                throw new ImportValidationException("The isPlanned column must contain true or false.");
            }

            var downtime = new Downtime
            {
                Id = row[2],
                EquipmentId = row[3],
                StartAt = start,
                EndAt = end,
                ReasonCode = row[6],
                IsPlanned = isPlanned,
                Comment = string.IsNullOrWhiteSpace(row[8]) ? null : row[8]
            };

            var record = new ImportRecordRequest
            {
                RecordType = nameof(Downtime),
                SourceRecordId = row[0],
                SourceRevision = row[1],
                SourceEventAt = start,
                Data = JsonSerializer.SerializeToElement(downtime, _serializerOptions)
            };

            records.Add(record);
        }

        return new ImportBatchRequest { SourceSystem = sourceSystem, Records = records };
    }

    /// <summary>
    /// Reads material consumption, return, and production output events from CSV.
    /// </summary>
    public ImportBatchRequest ParseMovements(string sourceSystem, string csv)
    {
        var headers = new[]
        {
            "sourceRecordId", "sourceRevision", "id", "executionId", "materialLotId", "kind",
            "quantity", "unitId", "occurredAt", "outputQuality", "reversesMovementId"
        };

        var rows = ReadRows(csv, headers);
        var records = new List<ImportRecordRequest>();

        foreach (var row in rows)
        {
            if (!Enum.TryParse<MaterialMovementKind>(row[5], true, out var kind)
                || !Enum.GetNames<MaterialMovementKind>().Contains(row[5], StringComparer.OrdinalIgnoreCase))
            {
                throw new ImportValidationException($"Unknown material movement kind '{row[5]}'.");
            }

            if (!decimal.TryParse(row[6], NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var quantity))
            {
                throw new ImportValidationException("Material quantities must use an invariant decimal format.");
            }

            OutputQuality? quality = null;

            if (!string.IsNullOrWhiteSpace(row[9]))
            {
                if (!Enum.TryParse<OutputQuality>(row[9], true, out var parsedQuality)
                    || !Enum.GetNames<OutputQuality>().Contains(row[9], StringComparer.OrdinalIgnoreCase))
                {
                    throw new ImportValidationException($"Unknown output quality '{row[9]}'.");
                }

                quality = parsedQuality;
            }

            var occurredAt = ParseDate(row[8], "occurredAt");

            var movement = new MaterialMovement
            {
                Id = row[2],
                ExecutionId = row[3],
                MaterialLotId = row[4],
                Kind = kind,
                Quantity = quantity,
                UnitId = row[7],
                OccurredAt = occurredAt,
                OutputQuality = quality,
                ReversesMovementId = string.IsNullOrWhiteSpace(row[10]) ? null : row[10]
            };

            var record = new ImportRecordRequest
            {
                RecordType = nameof(MaterialMovement),
                SourceRecordId = row[0],
                SourceRevision = row[1],
                SourceEventAt = occurredAt,
                Data = JsonSerializer.SerializeToElement(movement, _serializerOptions)
            };

            records.Add(record);
        }

        return new ImportBatchRequest { SourceSystem = sourceSystem, Records = records };
    }

    private List<string[]> ReadRows(string csv, string[] expectedHeaders)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            throw new ImportValidationException("The CSV document cannot be empty.");
        }

        using var parser = new TextFieldParser(new StringReader(csv))
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = true
        };

        parser.SetDelimiters(",");

        try
        {
            var actualHeaders = parser.ReadFields();

            if (actualHeaders is null || !actualHeaders.SequenceEqual(expectedHeaders, StringComparer.OrdinalIgnoreCase))
            {
                throw new ImportValidationException($"CSV columns must be: {string.Join(",", expectedHeaders)}.");
            }

            var rows = new List<string[]>();

            while (!parser.EndOfData)
            {
                var fields = parser.ReadFields();

                if (fields is null || fields.Length != expectedHeaders.Length)
                {
                    throw new ImportValidationException($"Invalid CSV column count near line {parser.LineNumber}.");
                }

                rows.Add(fields);

                if (rows.Count > 500)
                {
                    throw new ImportValidationException("CSV imports cannot contain more than 500 records.");
                }
            }

            return rows;
        }
        catch (MalformedLineException exception)
        {
            throw new ImportValidationException($"Malformed CSV near line {exception.LineNumber}.");
        }
    }

    private DateTimeOffset ParseDate(string value, string name)
    {
        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            throw new ImportValidationException($"The {name} column must contain an ISO 8601 timestamp.");
        }

        return parsed;
    }
}
