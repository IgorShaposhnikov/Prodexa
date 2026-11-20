namespace Prodexa.Importing.Models;

/// <summary>
/// Summarizes an atomic source import without exposing database implementation details.
/// </summary>
public sealed class ImportBatchResult
{
    /// <summary>
    /// System that provided the imported records.
    /// </summary>
    public required string SourceSystem { get; init; }

    /// <summary>
    /// Number of new canonical records and source revisions stored.
    /// </summary>
    public required int ImportedCount { get; init; }

    /// <summary>
    /// Number of previously imported, unchanged source revisions.
    /// </summary>
    public required int SkippedCount { get; init; }
}
