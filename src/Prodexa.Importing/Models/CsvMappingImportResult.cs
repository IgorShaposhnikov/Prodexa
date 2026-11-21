namespace Prodexa.Importing.Models;

/// <summary>
/// Reports the outcome of an imported CSV, committed in batches.
/// </summary>
public sealed class CsvMappingImportResult
{
    /// <summary>
    /// Number of input data rows examined.
    /// </summary>
    public required int RowsScanned { get; init; }

    /// <summary>
    /// Rows excluded by configured filters.
    /// </summary>
    public required int FilteredRows { get; init; }

    /// <summary>
    /// Newly stored manufacturing records.
    /// </summary>
    public required int ImportedCount { get; init; }

    /// <summary>
    /// Unchanged previously imported source revisions.
    /// </summary>
    public required int SkippedCount { get; init; }
}
