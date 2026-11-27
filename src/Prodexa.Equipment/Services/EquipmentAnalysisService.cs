using Microsoft.EntityFrameworkCore;
using Prodexa.Core.Resources;
using Prodexa.Equipment.Models;
using Prodexa.Persistence;

namespace Prodexa.Equipment.Services;

/// <summary>
/// Builds equipment downtime reports from events persisted in PostgreSQL.
/// </summary>
public sealed class EquipmentAnalysisService(ProdexaDbContext context, DowntimeCalculator calculator)
{
    /// <summary>
    /// Calculates downtime and availability for an existing equipment position.
    /// </summary>
    /// <param name="equipmentId">Equipment position to analyze.</param>
    /// <param name="periodStart">Inclusive start of the reporting period.</param>
    /// <param name="periodEnd">Exclusive end of the reporting period.</param>
    /// <param name="cancellationToken">Token used to cancel database operations.</param>
    /// <returns>A report, or null when the equipment position does not exist.</returns>
    public async Task<EquipmentDowntimeReport?> GetDowntimeReportAsync(string equipmentId, DateTimeOffset periodStart, DateTimeOffset periodEnd, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(equipmentId);

        if (periodEnd <= periodStart)
        {
            throw new ArgumentException("The reporting window must have a positive duration.", nameof(periodEnd));
        }

        var exists = await context.Equipment.AsNoTracking()
            .AnyAsync(equipment => equipment.Id == equipmentId, cancellationToken);

        if (!exists)
        {
            return null;
        }

        // Normalize query bounds because PostgreSQL timestamp with time zone uses UTC instants.
        var periodStartUtc = periodStart.ToUniversalTime();
        var periodEndUtc = periodEnd.ToUniversalTime();

        var downtimes = await context.Downtimes.AsNoTracking()
            .Where(downtime => downtime.EquipmentId == equipmentId
                && downtime.StartAt < periodEndUtc
                && (downtime.EndAt == null || downtime.EndAt > periodStartUtc))
            .ToListAsync(cancellationToken);

        var downtimeDuration = calculator.CalculateTotalDuration(downtimes, equipmentId, periodStartUtc, periodEndUtc);
        var plannedDuration = calculator.CalculateTotalDuration(
            downtimes.Where(downtime => downtime.IsPlanned),
            equipmentId,
            periodStartUtc,
            periodEndUtc);

        var unplannedDuration = calculator.CalculateTotalDuration(
            downtimes.Where(downtime => !downtime.IsPlanned),
            equipmentId,
            periodStartUtc,
            periodEndUtc);

        var reportingDuration = periodEndUtc - periodStartUtc;
        var availabilityPercent = 100m * (reportingDuration.Ticks - downtimeDuration.Ticks) / reportingDuration.Ticks;

        return new EquipmentDowntimeReport
        {
            EquipmentId = equipmentId,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            EventCount = downtimes.Count,
            DowntimeMinutes = downtimeDuration.Ticks / (decimal)TimeSpan.TicksPerMinute,
            PlannedDowntimeMinutes = plannedDuration.Ticks / (decimal)TimeSpan.TicksPerMinute,
            UnplannedDowntimeMinutes = unplannedDuration.Ticks / (decimal)TimeSpan.TicksPerMinute,
            AvailabilityPercent = Math.Round(availabilityPercent, 2)
        };
    }
}
