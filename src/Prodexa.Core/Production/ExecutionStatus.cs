namespace Prodexa.Core.Production;

/// <summary>
/// Describes the state of an execution.
/// </summary>
public enum ExecutionStatus
{
    /// <summary>
    /// Execution has started.
    /// </summary>
    Started,
    /// <summary>
    /// Execution is complete.
    /// </summary>
    Completed,
    /// <summary>
    /// Execution was stopped without completion.
    /// </summary>
    Aborted
}
