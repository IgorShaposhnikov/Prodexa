namespace Prodexa.Production.Models;

/// <summary>
/// Describes an unfinished production task after its plan deadline.
/// </summary>
public sealed class OverdueProductionTask
{
    /// <summary>
    /// Stable identifier of the production task.
    /// </summary>
    public required string TaskId { get; init; }

    /// <summary>
    /// Identifier of the plan revision effective at the reporting time.
    /// </summary>
    public required string RevisionId { get; init; }

    /// <summary>
    /// Number of the effective plan revision.
    /// </summary>
    public required int RevisionNumber { get; init; }

    /// <summary>
    /// Identifier of the planned output product.
    /// </summary>
    public required string ProductId { get; init; }

    /// <summary>
    /// Planned quantity in the revision's unit.
    /// </summary>
    public required decimal PlannedQuantity { get; init; }

    /// <summary>
    /// Accepted good output from executions of this task before the reporting time.
    /// </summary>
    public required decimal AcceptedGoodOutput { get; init; }

    /// <summary>
    /// Unfulfilled quantity after counting accepted output.
    /// </summary>
    public required decimal RemainingQuantity { get; init; }

    /// <summary>
    /// Measurement unit shared by the plan and counted output events.
    /// </summary>
    public required string UnitId { get; init; }

    /// <summary>
    /// Deadline from the effective plan revision.
    /// </summary>
    public required DateTimeOffset DueAt { get; init; }

    /// <summary>
    /// Point in time at which the report was calculated.
    /// </summary>
    public required DateTimeOffset AsOf { get; init; }

    /// <summary>
    /// Minutes elapsed between the plan deadline and the reporting time.
    /// </summary>
    public required decimal OverdueMinutes { get; init; }
}
