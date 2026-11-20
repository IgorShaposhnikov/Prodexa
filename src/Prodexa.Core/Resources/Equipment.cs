namespace Prodexa.Core.Resources;

// The identifier remains stable even when its description or assigned area changes.
/// <summary>
/// Identifies an equipment position independently of its changing attributes.
/// </summary>
public sealed class Equipment
{
    /// <summary>
    /// Stable identifier of this record.
    /// </summary>
    public required string Id { get; init; }
}
