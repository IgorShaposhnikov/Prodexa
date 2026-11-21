namespace Prodexa.Importing.Models;

/// <summary>
/// Converted records displayed before a CSV import is committed.
/// </summary>
public sealed class CsvMappingPreviewResult
{
    /// <summary>
    /// Number of source rows examined to produce the preview.
    /// </summary>
    public required int RowsScanned { get; init; }

    /// <summary>
    /// Number of examined rows excluded by configured filters.
    /// </summary>
    public required int FilteredRows { get; init; }

    /// <summary>
    /// Up to twenty converted manufacturing records; no database writes are performed.
    /// </summary>
    public required IReadOnlyList<ImportRecordRequest> Records { get; init; }
}
