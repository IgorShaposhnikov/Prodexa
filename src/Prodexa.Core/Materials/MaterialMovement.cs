namespace Prodexa.Core.Materials;

/// <summary>
/// Records material consumed, returned, or produced during an execution.
/// </summary>
public sealed class MaterialMovement
{
    /// <summary>
    /// Stable identifier of this record.
    /// </summary>
    public required string Id { get; init; }
    /// <summary>
    /// Identifier of the associated production execution.
    /// </summary>
    public required string ExecutionId { get; init; }
    /// <summary>
    /// Identifier of the material lot involved in the movement.
    /// </summary>
    public required string MaterialLotId { get; init; }
    /// <summary>
    /// Kind of material movement.
    /// </summary>
    public required MaterialMovementKind Kind { get; init; }
    /// <summary>
    /// Positive movement quantity; the movement kind defines its direction.
    /// </summary>
    public required decimal Quantity { get; init; }
    /// <summary>
    /// Unit used for the recorded quantity.
    /// </summary>
    public required string UnitId { get; init; }
    /// <summary>
    /// Time at which the movement occurred.
    /// </summary>
    public required DateTimeOffset OccurredAt { get; init; }
    /// <summary>
    /// Quality classification for output movements.
    /// </summary>
    public OutputQuality? OutputQuality { get; init; }
    /// <summary>
    /// Identifier of the original consumption event for this return.
    /// </summary>
    public string? ReversesMovementId { get; init; }
}
