namespace Prodexa.Core.Resources;

/// <summary>
/// Describes an individual physical asset used in production.
/// </summary>
public sealed class PhysicalAsset
{
    /// <summary>
    /// Stable identifier of this record.
    /// </summary>
    public required string Id { get; init; }
    /// <summary>
    /// Display name associated with this version or asset.
    /// </summary>
    public required string Name { get; init; }
    /// <summary>
    /// Asset manufacturer, when supplied.
    /// </summary>
    public string? Manufacturer { get; init; }
    /// <summary>
    /// Manufacturer serial number; not assumed globally unique.
    /// </summary>
    public string? SerialNumber { get; init; }
    /// <summary>
    /// Lifecycle status of this resource.
    /// </summary>
    public required LifecycleStatus Status { get; init; }
}
