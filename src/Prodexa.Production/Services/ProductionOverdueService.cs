using Microsoft.EntityFrameworkCore;
using Prodexa.Core.Materials;
using Prodexa.Core.Production;
using Prodexa.Persistence;
using Prodexa.Production.Models;

namespace Prodexa.Production.Services;

/// <summary>
/// Identifies overdue production tasks using effective plans and accepted output events.
/// </summary>
public sealed class ProductionOverdueService(ProdexaDbContext context)
{
    /// <summary>
    /// Finds tasks whose effective plan is overdue and not fulfilled by accepted production output.
    /// </summary>
    /// <param name="asOf">Reporting instant used for plans and output events.</param>
    /// <param name="cancellationToken">Cancels PostgreSQL queries.</param>
    /// <returns>Overdue tasks with their remaining production quantities.</returns>
    public async Task<IReadOnlyList<OverdueProductionTask>> GetOverdueTasksAsync(DateTimeOffset asOf, CancellationToken cancellationToken)
    {
        var asOfUtc = asOf.ToUniversalTime();

        var revisions = await context.PlanTaskRevisions.AsNoTracking()
            .Where(revision => revision.EffectiveFrom <= asOfUtc
                && (revision.EffectiveTo == null || revision.EffectiveTo > asOfUtc))
            .OrderBy(revision => revision.TaskId)
            .ThenByDescending(revision => revision.EffectiveFrom)
            .ThenByDescending(revision => revision.RevisionNumber)
            .ToListAsync(cancellationToken);

        // Choose the most recent effective revision before checking status or deadline.
        var selectedTaskIds = new HashSet<string>(StringComparer.Ordinal);
        var overdueCandidates = new List<PlanTaskRevision>();

        foreach (var revision in revisions)
        {
            if (!selectedTaskIds.Add(revision.TaskId))
            {
                continue;
            }

            if (revision.Status == PlanTaskStatus.Cancelled || revision.DueAt >= asOfUtc)
            {
                continue;
            }

            overdueCandidates.Add(revision);
        }

        if (overdueCandidates.Count == 0)
        {
            return Array.Empty<OverdueProductionTask>();
        }

        var taskIds = overdueCandidates.Select(revision => revision.TaskId).ToArray();

        // Count only good output of the planned product and unit, across all task revisions.
        var acceptedOutput = await (
            from movement in context.MaterialMovements.AsNoTracking()
            join execution in context.Executions.AsNoTracking() on movement.ExecutionId equals execution.Id
            join lot in context.MaterialLots.AsNoTracking() on movement.MaterialLotId equals lot.Id
            where taskIds.Contains(execution.TaskId)
                && execution.StartAt <= asOfUtc
                && movement.OccurredAt <= asOfUtc
                && movement.Kind == MaterialMovementKind.Output
                && movement.OutputQuality == OutputQuality.Good
            group movement by new { execution.TaskId, lot.MaterialId, movement.UnitId } into outputGroup
            select new
            {
                outputGroup.Key.TaskId,
                outputGroup.Key.MaterialId,
                outputGroup.Key.UnitId,
                Quantity = outputGroup.Sum(movement => movement.Quantity)
            })
            .ToListAsync(cancellationToken);

        var reports = new List<OverdueProductionTask>();

        foreach (var revision in overdueCandidates)
        {
            var goodOutput = acceptedOutput
                .Where(output => output.TaskId == revision.TaskId
                    && output.MaterialId == revision.ProductId
                    && output.UnitId == revision.UnitId)
                .Sum(output => output.Quantity);

            if (!revision.IsOverdue(asOfUtc, goodOutput))
            {
                continue;
            }

            var report = new OverdueProductionTask
            {
                TaskId = revision.TaskId,
                RevisionId = revision.Id,
                RevisionNumber = revision.RevisionNumber,
                ProductId = revision.ProductId,
                PlannedQuantity = revision.PlannedQuantity,
                AcceptedGoodOutput = goodOutput,
                RemainingQuantity = revision.GetRemainingQuantity(goodOutput),
                UnitId = revision.UnitId,
                DueAt = revision.DueAt,
                AsOf = asOfUtc,
                OverdueMinutes = (asOfUtc - revision.DueAt).Ticks / (decimal)TimeSpan.TicksPerMinute
            };

            reports.Add(report);
        }

        return reports.OrderBy(report => report.DueAt).ThenBy(report => report.TaskId).ToList();
    }
}
