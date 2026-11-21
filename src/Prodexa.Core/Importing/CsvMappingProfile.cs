namespace Prodexa.Core.Importing;

/// <summary>
/// Stores a reusable CSV-to-manufacturing-model mapping definition.
/// </summary>
public sealed class CsvMappingProfile
{
    /// <summary>
    /// Stable user-assigned identifier of the mapping profile.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Human-readable profile name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Name of the upstream system used for idempotency.
    /// </summary>
    public required string SourceSystem { get; init; }

    /// <summary>
    /// Name of the target manufacturing model.
    /// </summary>
    public required string Target { get; init; }

    /// <summary>
    /// Full JSON definition of the mapping and its transformations.
    /// </summary>
    public required string DefinitionJson { get; init; }

    /// <summary>
    /// Instant at which the mapping profile was saved.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }
}
