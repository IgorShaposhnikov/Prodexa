namespace Prodexa.Core.Production;

/// <summary>
/// Identifies a reusable production process definition.
/// </summary>
public sealed class ProcessSegment
{
    /// <summary>
    /// Stable identifier of this record.
    /// </summary>
    public required string Id { get; init; }
}
