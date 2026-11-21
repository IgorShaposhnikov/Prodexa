using System.Text.Json;

namespace Prodexa.Importing.Models;

/// <summary>
/// Describes one target property as a source column, constant, or generated identifier.
/// </summary>
public sealed class CsvFieldMapping
{
    /// <summary>
    /// CSV column name supplying the original value.
    /// </summary>
    public string? Column { get; init; }

    /// <summary>
    /// Literal value used instead of a CSV column.
    /// </summary>
    public JsonElement? Constant { get; init; }

    /// <summary>
    /// Target type: string, boolean, integer, decimal, enum, or datetime_utc.
    /// </summary>
    public string? ConvertTo { get; init; }

    /// <summary>
    /// Input time encoding: iso8601, utc_naive, unix_seconds, or unix_milliseconds.
    /// </summary>
    public string? SourceFormat { get; init; }

    /// <summary>
    /// Compares a source column to this value and writes a boolean result.
    /// </summary>
    public string? Equals { get; init; }

    /// <summary>
    /// Set to stable_id to generate a reproducible identifier for the target property.
    /// </summary>
    public string? Generate { get; init; }
}
