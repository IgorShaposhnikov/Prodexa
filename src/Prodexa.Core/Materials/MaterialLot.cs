namespace Prodexa.Core.Materials;

/// <summary>
/// Identifies a specific lot of raw material, intermediate product, or output.
/// </summary>
public sealed class MaterialLot
{
    /// <summary>
    /// Stable identifier of this record.
    /// </summary>
    public required string Id { get; init; }
    /// <summary>
    /// Identifier of the material type.
    /// </summary>
    public required string MaterialId { get; init; }
    /// <summary>
    /// Time when the material lot was created.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }
    /// <summary>
    /// Optional starting quantity for this lot.
    /// </summary>
    public decimal? InitialQuantity { get; init; }
    /// <summary>
    /// Unit used for the recorded quantity.
    /// </summary>
    public string? UnitId { get; init; }
    /// <summary>
    /// Lifecycle status of the material lot.
    /// </summary>
    public required string Status { get; init; }
}
