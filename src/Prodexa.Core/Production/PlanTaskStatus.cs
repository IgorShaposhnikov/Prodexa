namespace Prodexa.Core.Production;

/// <summary>
/// Describes the lifecycle state of a planned production task.
/// </summary>
public enum PlanTaskStatus
{
    /// <summary>
    /// Task has been planned but not started.
    /// </summary>
    Planned,
    /// <summary>
    /// Task is currently active.
    /// </summary>
    Active,
    /// <summary>
    /// Task is marked completed.
    /// </summary>
    Completed,
    /// <summary>
    /// Task was cancelled and cannot be overdue.
    /// </summary>
    Cancelled
}
