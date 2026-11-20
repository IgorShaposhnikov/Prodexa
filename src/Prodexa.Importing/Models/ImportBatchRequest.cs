namespace Prodexa.Importing.Models;

/// <summary>
/// Defines a group of source records imported in a single database transaction.
/// </summary>
public sealed class ImportBatchRequest
{
    /// <summary>
    /// Unique name of the system that supplied the records.
    /// </summary>
    public required string SourceSystem { get; init; }

    /// <summary>
    /// Source records ordered so their dependencies are easy to follow.
    /// </summary>
    public required List<ImportRecordRequest> Records { get; init; }
}
