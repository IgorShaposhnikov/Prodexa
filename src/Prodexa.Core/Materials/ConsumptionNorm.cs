namespace Prodexa.Core.Materials;

/// <summary>
/// Defines the versioned material consumption rate for a product and process segment.
/// </summary>
public sealed class ConsumptionNorm
{
    /// <summary>
    /// Stable identifier of this record.
    /// </summary>
    public required string Id { get; init; }
    /// <summary>
    /// Stable identifier of the underlying norm.
    /// </summary>
    public required string NormId { get; init; }
    /// <summary>
    /// Number of this norm or material version.
    /// </summary>
    public required int VersionNumber { get; init; }
    /// <summary>
    /// Product definition version to which the norm applies.
    /// </summary>
    public required string ProductVersionId { get; init; }
    /// <summary>
    /// Input material definition version to which the norm applies.
    /// </summary>
    public required string MaterialVersionId { get; init; }
    /// <summary>
    /// Process definition version to which the norm applies.
    /// </summary>
    public required string ProcessSegmentVersionId { get; init; }
    /// <summary>
    /// Input material quantity allowed per unit of accepted output.
    /// </summary>
    public required decimal QuantityPerOutput { get; init; }
    /// <summary>
    /// Measurement unit for the consumed material.
    /// </summary>
    public required string InputUnitId { get; init; }
    /// <summary>
    /// Measurement unit for the resulting product.
    /// </summary>
    public required string OutputUnitId { get; init; }
    /// <summary>
    /// Inclusive start of this version's validity period.
    /// </summary>
    public required DateTimeOffset ValidFrom { get; init; }
    /// <summary>
    /// Exclusive end of this validity period, if specified.
    /// </summary>
    public DateTimeOffset? ValidTo { get; init; }
}
