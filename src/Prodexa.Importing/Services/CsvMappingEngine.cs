using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Prodexa.Importing.Models;

namespace Prodexa.Importing.Services;

/// <summary>
/// Converts arbitrary CSV rows into typed, idempotent manufacturing source records.
/// </summary>
public sealed class CsvMappingEngine(CsvTargetCatalog catalog)
{
    /// <summary>
    /// Checks that the input CSV contains all columns referenced by a mapping profile.
    /// </summary>
    public void ValidateColumns(CsvMappingDefinition definition, IReadOnlyList<string> columns)
    {
        var available = new HashSet<string>(columns, StringComparer.Ordinal);
        var required = definition.Fields.Values
            .Where(mapping => mapping.Column is not null)
            .Select(mapping => mapping.Column!)
            .Concat(definition.Filters.Keys);

        if (definition.SourceRecordIdColumn is not null)
        {
            required = required.Append(definition.SourceRecordIdColumn);
        }

        foreach (var column in required)
        {
            if (!available.Contains(column))
            {
                throw new ImportValidationException($"CSV is missing mapped column '{column}'.");
            }
        }
    }

    /// <summary>
    /// Applies source filters and converts a source row into the target model.
    /// Returns null for rows excluded by the profile.
    /// </summary>
    public ImportRecordRequest? MapRow(
        CsvMappingDefinition definition,
        IReadOnlyDictionary<string, string> row,
        string datasetId,
        int rowNumber)
    {
        foreach (var (column, allowedValues) in definition.Filters)
        {
            if (!allowedValues.Contains(row[column], StringComparer.Ordinal))
            {
                return null;
            }
        }

        var properties = catalog.Resolve(definition.Target).GetProperties()
            .ToDictionary(property => JsonNamingPolicy.CamelCase.ConvertName(property.Name), StringComparer.Ordinal);

        var identity = CreateStableIdentity(definition, datasetId, rowNumber);
        var converted = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var (propertyName, binding) in definition.Fields)
        {
            var propertyType = Nullable.GetUnderlyingType(properties[propertyName].PropertyType)
                ?? properties[propertyName].PropertyType;

            var value = binding.Generate == "stable_id"
                ? identity
                : binding.Column is not null
                    ? row[binding.Column]
                    : binding.Constant!.Value.ToString();

            if (binding.Equals is not null)
            {
                converted.Add(propertyName, string.Equals(value, binding.Equals, StringComparison.Ordinal));
                continue;
            }

            if (value is null || (value.Length == 0 && binding.Column is not null))
            {
                if (Nullable.GetUnderlyingType(properties[propertyName].PropertyType) is not null
                    || propertyType == typeof(string) && !properties[propertyName].IsDefined(typeof(System.Runtime.CompilerServices.RequiredMemberAttribute), true))
                {
                    converted.Add(propertyName, null);
                    continue;
                }

                throw new ImportValidationException($"Row {rowNumber}: required field '{propertyName}' is empty.");
            }

            converted.Add(propertyName, ConvertValue(value, propertyType, binding.SourceFormat, propertyName, rowNumber));
        }

        var sourceId = definition.SourceRecordIdColumn is null
            ? identity
            : row[definition.SourceRecordIdColumn];

        if (string.IsNullOrWhiteSpace(sourceId) || sourceId.Length > 100)
        {
            throw new ImportValidationException($"Row {rowNumber}: invalid source record identifier.");
        }

        DateTimeOffset? sourceEventAt = null;

        foreach (var key in new[] { "occurredAt", "startAt" })
        {
            if (converted.TryGetValue(key, out var date) && date is DateTimeOffset eventAt)
            {
                sourceEventAt = eventAt;
                break;
            }
        }

        return new ImportRecordRequest
        {
            RecordType = definition.Target,
            SourceRecordId = sourceId,
            SourceRevision = definition.SourceRevision,
            SourceEventAt = sourceEventAt,
            Data = JsonSerializer.SerializeToElement(converted)
        };
    }

    private object ConvertValue(string value, Type type, string? sourceFormat, string field, int rowNumber)
    {
        if (type == typeof(string))
        {
            return value;
        }

        if (type == typeof(bool))
        {
            if (bool.TryParse(value, out var boolean))
            {
                return boolean;
            }
        }
        else if (type == typeof(int))
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
            {
                return integer;
            }
        }
        else if (type == typeof(decimal))
        {
            if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
            {
                return number;
            }
        }
        else if (type.IsEnum)
        {
            var name = Enum.GetNames(type).FirstOrDefault(candidate => string.Equals(candidate, value, StringComparison.OrdinalIgnoreCase));

            if (name is not null)
            {
                return name;
            }
        }
        else if (type == typeof(DateTimeOffset))
        {
            if (sourceFormat is "unix_seconds" or "unix_milliseconds")
            {
                if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var epoch))
                {
                    try
                    {
                        return sourceFormat == "unix_seconds"
                            ? DateTimeOffset.FromUnixTimeSeconds(epoch)
                            : DateTimeOffset.FromUnixTimeMilliseconds(epoch);
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        // The timestamp is invalid and will be reported with its source row.
                    }
                }
            }
            else if (sourceFormat == "utc_naive")
            {
                if (DateTimeOffset.TryParse(
                    value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var interpreted))
                {
                    return interpreted;
                }
            }
            else if (Regex.IsMatch(value, @"(?:Z|[+-]\d{2}:\d{2})$", RegexOptions.IgnoreCase)
                && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var timestamp))
            {
                return timestamp.ToUniversalTime();
            }
        }

        throw new ImportValidationException($"Row {rowNumber}: cannot convert '{field}' value '{value}' to {catalog.GetTypeName(type)}.");
    }

    private string CreateStableIdentity(CsvMappingDefinition definition, string datasetId, int rowNumber)
    {
        var key = $"{definition.SourceSystem}\n{definition.Id}\n{definition.Target}\n{datasetId}\n{rowNumber}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));

        return $"csv-{Convert.ToHexString(bytes)[..32]}";
    }
}
