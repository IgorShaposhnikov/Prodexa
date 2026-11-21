namespace Prodexa.Importing.Models;

/// <summary>
/// Defines how CSV columns become one type of manufacturing record.
/// </summary>
public sealed class CsvMappingDefinition
{
    /// <summary>
    /// Stable identifier of this reusable mapping profile.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Human-readable profile name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Upstream system name used in source record keys.
    /// </summary>
    public required string SourceSystem { get; init; }

    /// <summary>
    /// Exact name of a supported Prodexa model, such as Downtime.
    /// </summary>
    public required string Target { get; init; }

    /// <summary>
    /// Optional CSV column containing a stable external record identifier.
    /// If omitted, a deterministic identifier is generated from the dataset key and row number.
    /// </summary>
    public string? SourceRecordIdColumn { get; init; }

    /// <summary>
    /// Version assigned to source records imported through this profile.
    /// </summary>
    public string SourceRevision { get; init; } = "1";

    /// <summary>
    /// Accepted source values for individual columns. Rows not matching are skipped.
    /// </summary>
    public Dictionary<string, List<string>> Filters { get; init; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Target property names mapped to CSV source expressions and type conversions.
    /// </summary>
    public required Dictionary<string, CsvFieldMapping> Fields { get; init; }
}
