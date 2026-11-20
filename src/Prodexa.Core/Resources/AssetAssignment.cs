namespace Prodexa.Core.Resources;

/// <summary>
/// Links a physical asset to an equipment position for a defined period.
/// </summary>
public sealed class AssetAssignment
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
    /// Identifier of the physical asset.
    /// </summary>
    public required string AssetId { get; init; }
    /// <summary>
    /// Inclusive start of the validity period.
    /// </summary>
    public required DateTimeOffset ValidFrom { get; init; }
    /// <summary>
    /// Exclusive end of the validity period, if specified.
    /// </summary>
    public DateTimeOffset? ValidTo { get; init; }
}
