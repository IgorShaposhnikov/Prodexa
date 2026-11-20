using System.Text.Json;

namespace Prodexa.Importing.Models;

/// <summary>
/// Identifies a source revision and its canonical manufacturing data.
/// </summary>
public sealed class ImportRecordRequest
{
    /// <summary>
    /// Name of the manufacturing model to import.
    /// </summary>
    public required string RecordType { get; init; }

    /// <summary>
    /// Identifier assigned to the record by its source system.
    /// </summary>
    public required string SourceRecordId { get; init; }

    /// <summary>
    /// Revision identifier supplied by the source.
    /// </summary>
    public required string SourceRevision { get; init; }

    /// <summary>
    /// Structured manufacturing record, using camelCase field names.
    /// </summary>
    public required JsonElement Data { get; init; }

    /// <summary>
    /// Original event timestamp, when known.
    /// </summary>
    public DateTimeOffset? SourceEventAt { get; init; }
}
