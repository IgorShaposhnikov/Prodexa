namespace Prodexa.Core.Resources;

/// <summary>
/// Distinguishes personnel from equipment when recording resource usage.
/// </summary>
public enum ResourceType
{
    /// <summary>
    /// Resource refers to equipment.
    /// </summary>
    Equipment,
    /// <summary>
    /// Resource refers to personnel.
    /// </summary>
    Person
}
