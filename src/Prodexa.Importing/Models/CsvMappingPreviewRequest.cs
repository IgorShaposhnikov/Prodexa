namespace Prodexa.Importing.Models;

/// <summary>
/// Supplies a mapping definition and a small CSV sample parsed by the client.
/// </summary>
public sealed class CsvMappingPreviewRequest
{
    /// <summary>
    /// Mapping to validate and apply; it does not need to be saved first.
    /// </summary>
    public required CsvMappingDefinition Mapping { get; init; }

    /// <summary>
    /// Stable source dataset key also used when importing the complete CSV.
    /// </summary>
    public required string DatasetId { get; init; }

    /// <summary>
    /// First one to twenty CSV data rows, keyed by the original column names.
    /// </summary>
    public required List<Dictionary<string, string>> Rows { get; init; }
}
