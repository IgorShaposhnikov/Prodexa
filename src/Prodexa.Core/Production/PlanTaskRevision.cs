namespace Prodexa.Core.Production;

/// <summary>
/// Preserves planned production values and their effective period.
/// </summary>
public sealed class PlanTaskRevision
{
    /// <summary>
    /// Stable identifier of this record.
    /// </summary>
    public required string Id { get; init; }
    /// <summary>
    /// Identifier of the associated production task.
    /// </summary>
    public required string TaskId { get; init; }
    /// <summary>
    /// Revision number within a production task.
    /// </summary>
    public required int RevisionNumber { get; init; }
    /// <summary>
    /// Identifier of the product planned for output.
    /// </summary>
    public required string ProductId { get; init; }
    /// <summary>
    /// Version of the process segment used for this plan.
    /// </summary>
    public required string ProcessSegmentVersionId { get; init; }
    /// <summary>
    /// Quantity planned for the revision.
    /// </summary>
    public required decimal PlannedQuantity { get; init; }
    /// <summary>
    /// Unit used for the planned quantity.
    /// </summary>
    public required string UnitId { get; init; }
    /// <summary>
    /// Deadline of the production task.
    /// </summary>
    public required DateTimeOffset DueAt { get; init; }
    /// <summary>
    /// Current lifecycle status.
    /// </summary>
    public required PlanTaskStatus Status { get; init; }
    /// <summary>
    /// Inclusive start of this revision's effective period.
    /// </summary>
    public required DateTimeOffset EffectiveFrom { get; init; }
    /// <summary>
    /// Exclusive end of this revision's effective period, if any.
    /// </summary>
    public DateTimeOffset? EffectiveTo { get; init; }
    /// <summary>
    /// Optional scheduled start of production.
    /// </summary>
    public DateTimeOffset? PlannedStartAt { get; init; }

    /// <summary>
    /// Calculates the remaining planned quantity without returning a negative value.
    /// </summary>
    /// <param name="acceptedGoodOutput">Accepted good output for this task.</param>
    /// <returns>Quantity still outstanding.</returns>
    public decimal GetRemainingQuantity(decimal acceptedGoodOutput)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(acceptedGoodOutput);

        return Math.Max(PlannedQuantity - acceptedGoodOutput, 0m);
    }

    /// <summary>
    /// Checks whether a non-cancelled task has passed its deadline with outstanding output.
    /// </summary>
    /// <param name="at">Point in time used for the check.</param>
    /// <param name="acceptedGoodOutput">Accepted output recorded by that time.</param>
    /// <returns>True when the task is overdue.</returns>
    public bool IsOverdue(DateTimeOffset at, decimal acceptedGoodOutput)
    {
        return Status != PlanTaskStatus.Cancelled
            && DueAt < at
            && GetRemainingQuantity(acceptedGoodOutput) > 0m;
    }
}
