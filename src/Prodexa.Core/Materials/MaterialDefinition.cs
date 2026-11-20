namespace Prodexa.Core.Materials;

/// <summary>
/// Identifies a material type independently of its description.
/// </summary>
public sealed class MaterialDefinition
{
    /// <summary>
    /// Stable identifier of this record.
    /// </summary>
    public required string Id { get; init; }
}
