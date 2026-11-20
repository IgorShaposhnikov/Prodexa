namespace Prodexa.Core.Materials;

/// <summary>
/// Defines the meaning of a material movement.
/// </summary>
public enum MaterialMovementKind
{
    /// <summary>
    /// Material consumed by production.
    /// </summary>
    Consume,
    /// <summary>
    /// Material returned after consumption.
    /// </summary>
    Return,
    /// <summary>
    /// Material produced by an execution.
    /// </summary>
    Output
}
