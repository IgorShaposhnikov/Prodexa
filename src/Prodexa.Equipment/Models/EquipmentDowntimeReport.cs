namespace Prodexa.Equipment.Models;

/// <summary>
/// Summarizes distinct downtime duration and equipment availability over a reporting window.
/// </summary>
public sealed class EquipmentDowntimeReport
{
    /// <summary>
    /// Identifier of the analyzed equipment position.
    /// </summary>
    public required string EquipmentId { get; init; }

    /// <summary>
    /// Inclusive start of the reporting window.
    /// </summary>
    public required DateTimeOffset PeriodStart { get; init; }

    /// <summary>
    /// Exclusive end of the reporting window.
    /// </summary>
    public required DateTimeOffset PeriodEnd { get; init; }

    /// <summary>
    /// Number of source downtime records intersecting the reporting window.
    /// </summary>
    public required int EventCount { get; init; }

    /// <summary>
    /// Minutes of downtime without double-counting overlapping source records.
    /// </summary>
    public required decimal DowntimeMinutes { get; init; }

    /// <summary>
    /// Minutes covered by planned downtime, counting overlapping planned intervals only once.
    /// </summary>
    public required decimal PlannedDowntimeMinutes { get; init; }

    /// <summary>
    /// Minutes covered by unplanned downtime, counting overlapping unplanned intervals only once.
    /// </summary>
    public required decimal UnplannedDowntimeMinutes { get; init; }

    /// <summary>
    /// Percentage of the reporting window not covered by downtime, rounded to two decimal places.
    /// </summary>
    public required decimal AvailabilityPercent { get; init; }
}
