namespace Prodexa.Core.Resources;

/// <summary>
/// Preserves the description and location of an equipment position over time.
/// </summary>
public sealed class EquipmentVersion
{
    /// <summary>
    /// Stable identifier of this record.
    /// </summary>
    public required string Id { get; init; }
    /// <summary>
    /// Identifier of the equipment position.
    /// </summary>
    public required string EquipmentId { get; init; }
    /// <summary>
    /// Version number of this equipment description.
    /// </summary>
    public required int VersionNumber { get; init; }
    /// <summary>
    /// Display name associated with this version or asset.
    /// </summary>
    public required string Name { get; init; }
    /// <summary>
    /// Class of equipment for this version.
    /// </summary>
    public required string EquipmentClassId { get; init; }
    /// <summary>
    /// Production area associated with this version.
    /// </summary>
    public required string AreaId { get; init; }
    /// <summary>
    /// Lifecycle status of this resource.
    /// </summary>
    public required LifecycleStatus Status { get; init; }
    /// <summary>
    /// Inclusive start of the validity period.
    /// </summary>
    public required DateTimeOffset ValidFrom { get; init; }
    /// <summary>
    /// Exclusive end of the validity period, if specified.
    /// </summary>
    public DateTimeOffset? ValidTo { get; init; }
    /// <summary>
    /// Reference to the original source record.
    /// </summary>
    public required string SourceRecordId { get; init; }
}
