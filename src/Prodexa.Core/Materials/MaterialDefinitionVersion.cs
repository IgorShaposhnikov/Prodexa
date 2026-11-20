namespace Prodexa.Core.Materials;

/// <summary>
/// Preserves a material type's description during a validity period.
/// </summary>
public sealed class MaterialDefinitionVersion
{
    /// <summary>
    /// Stable identifier of this record.
    /// </summary>
    public required string Id { get; init; }
    /// <summary>
    /// Identifier of the material type.
    /// </summary>
    public required string MaterialId { get; init; }
    /// <summary>
    /// Number of this norm or material version.
    /// </summary>
    public required int VersionNumber { get; init; }
    /// <summary>
    /// Display name during this version's validity period.
    /// </summary>
    public required string Name { get; init; }
    /// <summary>
    /// Classification of the material type.
    /// </summary>
    public required string MaterialClassId { get; init; }
    /// <summary>
    /// Accounting unit used for this material type.
    /// </summary>
    public required string BaseUnitId { get; init; }
    /// <summary>
    /// Inclusive start of this version's validity period.
    /// </summary>
    public required DateTimeOffset ValidFrom { get; init; }
    /// <summary>
    /// Exclusive end of this validity period, if specified.
    /// </summary>
    public DateTimeOffset? ValidTo { get; init; }
    /// <summary>
    /// External record identifier within the source and record type.
    /// </summary>
    public required string SourceRecordId { get; init; }
}
