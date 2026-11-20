namespace Prodexa.Core.Production;

/// <summary>
/// Captures the attributes of a process segment during a validity period.
/// </summary>
public sealed class ProcessSegmentVersion
{
    /// <summary>
    /// Stable identifier of this record.
    /// </summary>
    public required string Id { get; init; }
    /// <summary>
    /// Stable identifier of the process definition.
    /// </summary>
    public required string ProcessSegmentId { get; init; }
    /// <summary>
    /// Version number of this process description.
    /// </summary>
    public required int VersionNumber { get; init; }
    /// <summary>
    /// Process segment name valid for this version.
    /// </summary>
    public required string Name { get; init; }
    /// <summary>
    /// Optional explanation of the manufacturing operation.
    /// </summary>
    public string? Description { get; init; }
    /// <summary>
    /// Equipment class required by this operation, if specified.
    /// </summary>
    public string? RequiredEquipmentClassId { get; init; }
    /// <summary>
    /// Personnel class required by this operation, if specified.
    /// </summary>
    public string? RequiredPersonnelClassId { get; init; }
    /// <summary>
    /// Inclusive start of this version's validity period.
    /// </summary>
    public required DateTimeOffset ValidFrom { get; init; }
    /// <summary>
    /// Exclusive end of this version's validity period, if any.
    /// </summary>
    public DateTimeOffset? ValidTo { get; init; }
    /// <summary>
    /// Source record from which this description was obtained.
    /// </summary>
    public required string SourceRecordId { get; init; }
}
