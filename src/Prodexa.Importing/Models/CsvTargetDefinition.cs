namespace Prodexa.Importing.Models;

/// <summary>
/// Describes a manufacturing model available for source-to-target mapping.
/// </summary>
public sealed class CsvTargetDefinition
{
    /// <summary>
    /// Model name accepted by the import API.
    /// </summary>
    public required string Target { get; init; }

    /// <summary>
    /// Properties that a client can bind to source columns.
    /// </summary>
    public required IReadOnlyList<CsvTargetField> Fields { get; init; }
}

/// <summary>
/// Describes a target property, its expected value type and allowed enum values.
/// </summary>
public sealed class CsvTargetField
{
    /// <summary>
    /// JSON property name used as a key in the mappings object.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Type accepted by this property.
    /// </summary>
    public required string ConvertTo { get; init; }

    /// <summary>
    /// Whether the target model declares this property as required.
    /// </summary>
    public required bool Required { get; init; }

    /// <summary>
    /// Allowed names for enum-valued properties.
    /// </summary>
    public IReadOnlyList<string>? Values { get; init; }
}
