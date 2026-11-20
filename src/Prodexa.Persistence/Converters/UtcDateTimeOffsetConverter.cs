using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Prodexa.Persistence.Converters;

/// <summary>
/// Converts source timestamps to UTC before writing them to PostgreSQL.
/// </summary>
public sealed class UtcDateTimeOffsetConverter : ValueConverter<DateTimeOffset, DateTimeOffset>
{
    /// <summary>
    /// Preserves the instant while normalizing non-zero source offsets to UTC.
    /// </summary>
    public UtcDateTimeOffsetConverter() : base(value => value.ToUniversalTime(), value => value)
    {
    }
}
