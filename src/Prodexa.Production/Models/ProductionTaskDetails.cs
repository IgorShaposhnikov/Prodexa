namespace Prodexa.Production.Models;

/// <summary>
/// Contains a production task and its available planning revisions.
/// </summary>
public sealed class ProductionTaskDetails
{
    /// <summary>
    /// Stable identifier of the production task.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Plan revisions ordered by revision number.
    /// </summary>
    public required IReadOnlyList<ProductionTaskRevision> Revisions { get; init; }
}
