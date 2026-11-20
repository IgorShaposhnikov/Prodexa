namespace Prodexa.Core.Resources;

/// <summary>
/// Represents a production participant using a non-personal identifier.
/// </summary>
public sealed class Person
{
    /// <summary>
    /// Stable identifier of this record.
    /// </summary>
    public required string Id { get; init; }
    /// <summary>
    /// Anonymous code identifying the participant.
    /// </summary>
    public required string DisplayCode { get; init; }
    /// <summary>
    /// Lifecycle status of this resource.
    /// </summary>
    public required LifecycleStatus Status { get; init; }
}
