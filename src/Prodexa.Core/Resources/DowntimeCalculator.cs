namespace Prodexa.Core.Resources;

/// <summary>
/// Calculates equipment downtime without counting overlapping intervals more than once.
/// </summary>
public sealed class DowntimeCalculator
{
    /// <summary>
    /// Returns the total downtime of an equipment position within the requested reporting window.
    /// </summary>
    /// <param name="downtimes">Recorded downtime intervals.</param>
    /// <param name="equipmentId">Equipment position to analyze.</param>
    /// <param name="periodStart">Inclusive start of the reporting window.</param>
    /// <param name="periodEnd">Exclusive end of the reporting window.</param>
    /// <returns>Duration covered by at least one matching downtime interval.</returns>
    public TimeSpan CalculateTotalDuration(IEnumerable<Downtime> downtimes, string equipmentId, DateTimeOffset periodStart, DateTimeOffset periodEnd)
    {
        ArgumentNullException.ThrowIfNull(downtimes);
        ArgumentException.ThrowIfNullOrWhiteSpace(equipmentId);

        if (periodEnd <= periodStart)
        {
            throw new ArgumentException("The reporting window must have a positive duration.", nameof(periodEnd));
        }

        var intervals = new List<DowntimeInterval>();

        foreach (var downtime in downtimes)
        {
            ArgumentNullException.ThrowIfNull(downtime);

            if (!string.Equals(downtime.EquipmentId, equipmentId, StringComparison.Ordinal))
            {
                continue;
            }

            if (downtime.EndAt.HasValue && downtime.EndAt.Value < downtime.StartAt)
            {
                throw new ArgumentException("Downtime cannot end before it starts.", nameof(downtimes));
            }

            // Open downtime is bounded by the end of the reporting window.
            var start = downtime.StartAt < periodStart ? periodStart : downtime.StartAt;
            var end = downtime.EndAt ?? periodEnd;

            if (end > periodEnd)
            {
                end = periodEnd;
            }

            if (start < end)
            {
                intervals.Add(new DowntimeInterval(start, end));
            }
        }

        if (intervals.Count == 0)
        {
            return TimeSpan.Zero;
        }

        intervals.Sort(CompareIntervals);

        var total = TimeSpan.Zero;
        var currentStart = intervals[0].Start;
        var currentEnd = intervals[0].End;

        for (var index = 1; index < intervals.Count; index++)
        {
            var interval = intervals[index];

            if (interval.Start <= currentEnd)
            {
                if (interval.End > currentEnd)
                {
                    currentEnd = interval.End;
                }

                continue;
            }

            total += currentEnd - currentStart;
            currentStart = interval.Start;
            currentEnd = interval.End;
        }

        return total + (currentEnd - currentStart);
    }

    private int CompareIntervals(DowntimeInterval left, DowntimeInterval right)
    {
        return left.Start.CompareTo(right.Start);
    }

    /// <summary>
    /// Represents a downtime interval clipped to the requested reporting window.
    /// </summary>
    private readonly struct DowntimeInterval
    {
        /// <summary>
        /// Inclusive beginning of the interval.
        /// </summary>
        public DateTimeOffset Start { get; }

        /// <summary>
        /// Exclusive end of the interval.
        /// </summary>
        public DateTimeOffset End { get; }

        public DowntimeInterval(DateTimeOffset start, DateTimeOffset end)
        {
            Start = start;
            End = end;
        }
    }
}
