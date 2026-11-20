namespace Prodexa.Core.Production;

/// <summary>
/// Identifies a production task independently of its revisions.
/// </summary>
public sealed class PlanTask
{
    /// <summary>
    /// Stable identifier of this record.
    /// </summary>
    public required string Id { get; init; }
}
