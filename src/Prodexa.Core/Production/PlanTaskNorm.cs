namespace Prodexa.Core.Production;

// Norms are assigned per material and task revision, not through a single norm ID on the task.
/// <summary>
/// Binds a material-specific consumption norm to a task revision.
/// </summary>
public sealed class PlanTaskNorm
{
    /// <summary>
    /// Identifier of the plan revision used during execution.
    /// </summary>
    public required string TaskRevisionId { get; init; }
    /// <summary>
    /// Material to which the consumption norm applies.
    /// </summary>
    public required string MaterialId { get; init; }
    /// <summary>
    /// Identifier of the assigned consumption norm version.
    /// </summary>
    public required string NormVersionId { get; init; }
}
