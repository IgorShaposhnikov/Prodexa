namespace Prodexa.Production.Models;

/// <summary>
/// Represents a planning revision returned by the production API.
/// </summary>
public sealed class ProductionTaskRevision
{
    /// <summary>
    /// Identifier of the plan revision.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Revision number within the production task.
    /// </summary>
    public required int RevisionNumber { get; init; }

    /// <summary>
    /// Identifier of the planned product.
    /// </summary>
    public required string ProductId { get; init; }

    /// <summary>
    /// Identifier of the process segment version used in this revision.
    /// </summary>
    public required string ProcessSegmentVersionId { get; init; }

    /// <summary>
    /// Planned quantity for this revision.
    /// </summary>
    public required decimal PlannedQuantity { get; init; }

    /// <summary>
    /// Unit of measurement of the planned quantity.
    /// </summary>
    public required string UnitId { get; init; }

    /// <summary>
    /// Deadline of this plan revision.
    /// </summary>
    public required DateTimeOffset DueAt { get; init; }

    /// <summary>
    /// Lifecycle status of this plan revision.
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Inclusive start of the revision validity period.
    /// </summary>
    public required DateTimeOffset EffectiveFrom { get; init; }

    /// <summary>
    /// Exclusive end of the revision validity period, when known.
    /// </summary>
    public DateTimeOffset? EffectiveTo { get; init; }
}
