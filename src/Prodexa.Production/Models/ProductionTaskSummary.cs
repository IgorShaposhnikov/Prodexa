namespace Prodexa.Production.Models;

/// <summary>
/// Represents a production task in a paginated task listing.
/// </summary>
public sealed class ProductionTaskSummary
{
    /// <summary>
    /// Stable identifier of the production task.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Number of recorded plan revisions for this task.
    /// </summary>
    public required int RevisionCount { get; init; }
}
