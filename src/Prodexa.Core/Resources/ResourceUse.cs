namespace Prodexa.Core.Resources;

/// <summary>
/// Records a resource's participation in a production execution.
/// </summary>
public sealed class ResourceUse
{
    /// <summary>
    /// Stable identifier of this record.
    /// </summary>
    public required string Id { get; init; }
    /// <summary>
    /// Identifier of the execution using this resource.
    /// </summary>
    public required string ExecutionId { get; init; }
    /// <summary>
    /// Kind of participating resource.
    /// </summary>
    public required ResourceType ResourceType { get; init; }
    /// <summary>
    /// Identifier of the participating equipment or person.
    /// </summary>
    public required string ResourceId { get; init; }
    /// <summary>
    /// Role of the resource during this execution.
    /// </summary>
    public required string RoleCode { get; init; }
    /// <summary>
    /// Inclusive start of the validity period.
    /// </summary>
    public required DateTimeOffset ValidFrom { get; init; }
    /// <summary>
    /// Exclusive end of the validity period, if specified.
    /// </summary>
    public DateTimeOffset? ValidTo { get; init; }
}
