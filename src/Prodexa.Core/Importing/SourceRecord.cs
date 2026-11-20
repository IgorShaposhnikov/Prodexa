namespace Prodexa.Core.Importing;

/// <summary>
/// Tracks the origin and revision of a manufacturing record received from an external source.
/// </summary>
public sealed class SourceRecord
{
    /// <summary>
    /// Name of the system providing this record.
    /// </summary>
    public required string SourceSystem { get; init; }
    /// <summary>
    /// Type of record defined by the source system.
    /// </summary>
    public required string RecordType { get; init; }
    /// <summary>
    /// External record identifier within the source and record type.
    /// </summary>
    public required string SourceRecordId { get; init; }
    /// <summary>
    /// Revision supplied by the source system.
    /// </summary>
    public required string SourceRevision { get; init; }
    /// <summary>
    /// Time when this record was received.
    /// </summary>
    public required DateTimeOffset ReceivedAt { get; init; }
    /// <summary>
    /// Source event timestamp, when available.
    /// </summary>
    public DateTimeOffset? SourceEventAt { get; init; }
    /// <summary>
    /// Canonical identifier of the referenced model object or event.
    /// </summary>
    public required string TargetId { get; init; }
    /// <summary>
    /// Hash used to detect duplicates and conflicting source revisions.
    /// </summary>
    public required string PayloadHash { get; init; }
}
