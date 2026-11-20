namespace Prodexa.Core.Resources;

/// <summary>
/// Records an interval during which equipment was unavailable.
/// </summary>
public sealed class Downtime
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
    /// Start time of the downtime interval.
    /// </summary>
    public required DateTimeOffset StartAt { get; init; }
    /// <summary>
    /// End time of downtime, if the interval is closed.
    /// </summary>
    public DateTimeOffset? EndAt { get; init; }
    /// <summary>
    /// Reason code for the downtime event.
    /// </summary>
    public required string ReasonCode { get; init; }
    /// <summary>
    /// Whether the downtime was scheduled.
    /// </summary>
    public required bool IsPlanned { get; init; }
    /// <summary>
    /// Optional additional context from the source.
    /// </summary>
    public string? Comment { get; init; }
}
