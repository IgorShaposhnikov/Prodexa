namespace Prodexa.Core.Resources;

/// <summary>
/// Records a participant's classification during a validity period.
/// </summary>
public sealed class PersonClassMembership
{
    /// <summary>
    /// Identifier of the production participant.
    /// </summary>
    public required string PersonId { get; init; }
    /// <summary>
    /// Classification assigned to the participant.
    /// </summary>
    public required string PersonnelClassId { get; init; }
    /// <summary>
    /// Inclusive start of the validity period.
    /// </summary>
    public required DateTimeOffset ValidFrom { get; init; }
    /// <summary>
    /// Exclusive end of the validity period, if specified.
    /// </summary>
    public DateTimeOffset? ValidTo { get; init; }
}
