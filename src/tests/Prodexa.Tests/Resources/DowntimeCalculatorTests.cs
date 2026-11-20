using Prodexa.Core.Resources;
using Xunit;

namespace Prodexa.Tests.Resources;

/// <summary>
/// Verifies time-window clipping and union of equipment downtime intervals.
/// </summary>
public sealed class DowntimeCalculatorTests
{
    private readonly DowntimeCalculator _calculator = new();

    /// <summary>
    /// Overlapping, adjacent, duplicate and disjoint intervals contribute their union.
    /// </summary>
    [Theory]
    [InlineData(10, 60, 60)]
    [InlineData(20, 45, 45)]
    [InlineData(0, 20, 20)]
    [InlineData(50, 70, 40)]
    public void CalculateTotalDuration_CombinesIntervals(int secondStartOffset, int secondEndOffset, int expectedMinutes)
    {
        var periodStart = At(8, 0);
        var downtimes = new[]
        {
            CreateDowntime("D-01", "EQ-01", At(7, 50), At(8, 20)),
            CreateDowntime("D-02", "EQ-01", periodStart.AddMinutes(secondStartOffset), periodStart.AddMinutes(secondEndOffset))
        };

        var total = _calculator.CalculateTotalDuration(downtimes, "EQ-01", periodStart, At(12, 0));

        Assert.Equal(TimeSpan.FromMinutes(expectedMinutes), total);
    }

    /// <summary>
    /// Identical source intervals must not increase the reported downtime.
    /// </summary>
    [Fact]
    public void CalculateTotalDuration_DoesNotCountDuplicateRecords()
    {
        var downtime = CreateDowntime("D-01", "EQ-01", At(8, 0), At(8, 20));
        var downtimes = new[] { downtime, downtime };

        var total = _calculator.CalculateTotalDuration(downtimes, "EQ-01", At(8, 0), At(12, 0));

        Assert.Equal(TimeSpan.FromMinutes(20), total);
    }

    /// <summary>
    /// Intervals can arrive out of chronological order.
    /// </summary>
    [Fact]
    public void CalculateTotalDuration_SortsDisjointIntervals()
    {
        var downtimes = new[]
        {
            CreateDowntime("D-02", "EQ-01", At(9, 0), At(9, 15)),
            CreateDowntime("D-01", "EQ-01", At(8, 10), At(8, 30))
        };

        var total = _calculator.CalculateTotalDuration(downtimes, "EQ-01", At(8, 0), At(12, 0));

        Assert.Equal(TimeSpan.FromMinutes(35), total);
    }

    /// <summary>
    /// Downtime from other equipment positions is excluded.
    /// </summary>
    [Fact]
    public void CalculateTotalDuration_FiltersEquipment()
    {
        var downtimes = new[]
        {
            CreateDowntime("D-01", "EQ-01", At(8, 0), At(8, 20)),
            CreateDowntime("D-02", "EQ-02", At(8, 0), At(9, 0))
        };

        var total = _calculator.CalculateTotalDuration(downtimes, "EQ-01", At(8, 0), At(12, 0));

        Assert.Equal(TimeSpan.FromMinutes(20), total);
    }

    /// <summary>
    /// Only the portion of an interval inside the reporting window is counted.
    /// </summary>
    [Theory]
    [InlineData(-60, 20, 20)]
    [InlineData(220, 300, 20)]
    [InlineData(-60, 300, 240)]
    [InlineData(-60, 0, 0)]
    [InlineData(240, 300, 0)]
    [InlineData(60, 90, 30)]
    public void CalculateTotalDuration_ClipsToReportingWindow(int startOffset, int endOffset, int expectedMinutes)
    {
        var periodStart = At(8, 0);
        var downtimes = new[]
        {
            CreateDowntime("D-01", "EQ-01", periodStart.AddMinutes(startOffset), periodStart.AddMinutes(endOffset))
        };

        var total = _calculator.CalculateTotalDuration(downtimes, "EQ-01", periodStart, At(12, 0));

        Assert.Equal(TimeSpan.FromMinutes(expectedMinutes), total);
    }

    /// <summary>
    /// An unfinished downtime interval ends at the reporting window boundary for calculation.
    /// </summary>
    [Theory]
    [InlineData(-60, 240)]
    [InlineData(90, 150)]
    [InlineData(240, 0)]
    public void CalculateTotalDuration_BoundsOpenIntervals(int startOffset, int expectedMinutes)
    {
        var periodStart = At(8, 0);
        var downtimes = new[]
        {
            CreateDowntime("D-01", "EQ-01", periodStart.AddMinutes(startOffset), null)
        };

        var total = _calculator.CalculateTotalDuration(downtimes, "EQ-01", periodStart, At(12, 0));

        Assert.Equal(TimeSpan.FromMinutes(expectedMinutes), total);
    }

    /// <summary>
    /// The reporting window must have a positive duration.
    /// </summary>
    [Theory]
    [InlineData(12, 8)]
    [InlineData(8, 8)]
    public void CalculateTotalDuration_RejectsInvalidReportingWindow(int startHour, int endHour)
    {
        var downtimes = Array.Empty<Downtime>();

        Assert.Throws<ArgumentException>(() => _calculator.CalculateTotalDuration(downtimes, "EQ-01", At(startHour, 0), At(endHour, 0)));
    }

    /// <summary>
    /// Invalid source intervals are rejected instead of yielding negative durations.
    /// </summary>
    [Fact]
    public void CalculateTotalDuration_RejectsNegativeDowntime()
    {
        var downtimes = new[]
        {
            CreateDowntime("D-01", "EQ-01", At(9, 0), At(8, 0))
        };

        Assert.Throws<ArgumentException>(() => _calculator.CalculateTotalDuration(downtimes, "EQ-01", At(8, 0), At(12, 0)));
    }

    private DateTimeOffset At(int hour, int minute)
    {
        return new DateTimeOffset(2026, 11, 1, hour, minute, 0, TimeSpan.FromHours(5));
    }

    private Downtime CreateDowntime(string id, string equipmentId, DateTimeOffset start, DateTimeOffset? end)
    {
        return new Downtime
        {
            Id = id,
            EquipmentId = equipmentId,
            StartAt = start,
            EndAt = end,
            ReasonCode = "Mechanical",
            IsPlanned = false
        };
    }
}
