namespace Prodexa.Core.Production;

/// <summary>
/// Records one execution of a production task against a fixed plan revision.
/// </summary>
public sealed class Execution
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
    /// Identifier of the plan revision used during execution.
    /// </summary>
    public required string TaskRevisionId { get; init; }
    /// <summary>
    /// Start time of the execution.
    /// </summary>
    public required DateTimeOffset StartAt { get; init; }
    /// <summary>
    /// End time, if the execution has finished.
    /// </summary>
    public DateTimeOffset? EndAt { get; init; }
    /// <summary>
    /// Current lifecycle status.
    /// </summary>
    public required ExecutionStatus Status { get; init; }
}
