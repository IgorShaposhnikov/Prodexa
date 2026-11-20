using Microsoft.EntityFrameworkCore;
using Prodexa.Core.Materials;
using Prodexa.Core.Production;
using Prodexa.Materials.Models;
using Prodexa.Persistence;

namespace Prodexa.Materials.Services;

/// <summary>
/// Calculates material consumption deviations using recorded production events and assigned norm versions.
/// </summary>
public sealed class MaterialConsumptionAnalysisService(ProdexaDbContext context)
{
    /// <summary>
    /// Compares consumption less returns against accepted good output for each task revision with an assigned norm.
    /// </summary>
    /// <param name="asOf">Latest instant included in the calculations.</param>
    /// <param name="cancellationToken">Cancellation token used for database queries.</param>
    /// <returns>Material variance reports for revisions that have production activity.</returns>
    public async Task<IReadOnlyList<MaterialConsumptionVariance>> GetConsumptionVarianceAsync(DateTimeOffset asOf, CancellationToken cancellationToken)
    {
        var asOfUtc = asOf.ToUniversalTime();

        var revisions = await context.PlanTaskRevisions.AsNoTracking()
            .Where(revision => revision.EffectiveFrom <= asOfUtc
                && (revision.EffectiveTo == null || revision.EffectiveTo > asOfUtc))
            .OrderBy(revision => revision.TaskId)
            .ThenByDescending(revision => revision.EffectiveFrom)
            .ThenByDescending(revision => revision.RevisionNumber)
            .ToListAsync(cancellationToken);

        // Select the effective revision before filtering cancelled tasks.
        var selectedTasks = new HashSet<string>(StringComparer.Ordinal);
        var activeRevisions = new Dictionary<string, PlanTaskRevision>(StringComparer.Ordinal);

        foreach (var revision in revisions)
        {
            if (!selectedTasks.Add(revision.TaskId) || revision.Status == PlanTaskStatus.Cancelled)
            {
                continue;
            }

            activeRevisions.Add(revision.Id, revision);
        }

        if (activeRevisions.Count == 0)
        {
            return Array.Empty<MaterialConsumptionVariance>();
        }

        var revisionIds = activeRevisions.Keys.ToArray();

        var bindings = await context.PlanTaskNorms.AsNoTracking()
            .Where(binding => revisionIds.Contains(binding.TaskRevisionId))
            .ToListAsync(cancellationToken);

        if (bindings.Count == 0)
        {
            return Array.Empty<MaterialConsumptionVariance>();
        }

        var normIds = bindings.Select(binding => binding.NormVersionId).Distinct().ToArray();

        var norms = await context.ConsumptionNorms.AsNoTracking()
            .Where(norm => normIds.Contains(norm.Id))
            .ToDictionaryAsync(norm => norm.Id, cancellationToken);

        var versionIds = norms.Values
            .SelectMany(norm => new[] { norm.ProductVersionId, norm.MaterialVersionId })
            .Distinct()
            .ToArray();

        var materialVersions = await context.MaterialDefinitionVersions.AsNoTracking()
            .Where(version => versionIds.Contains(version.Id))
            .ToDictionaryAsync(version => version.Id, cancellationToken);

        // Group data by the exact execution revision, material, movement type and unit.
        var movementTotals = await (
            from movement in context.MaterialMovements.AsNoTracking()
            join execution in context.Executions.AsNoTracking() on movement.ExecutionId equals execution.Id
            join lot in context.MaterialLots.AsNoTracking() on movement.MaterialLotId equals lot.Id
            where revisionIds.Contains(execution.TaskRevisionId)
                && execution.StartAt <= asOfUtc
                && movement.OccurredAt <= asOfUtc
                && (movement.Kind != MaterialMovementKind.Output || movement.OutputQuality == OutputQuality.Good)
            group movement by new { execution.TaskRevisionId, lot.MaterialId, movement.UnitId, movement.Kind } into movements
            select new
            {
                movements.Key.TaskRevisionId,
                movements.Key.MaterialId,
                movements.Key.UnitId,
                movements.Key.Kind,
                Quantity = movements.Sum(movement => movement.Quantity)
            })
            .ToListAsync(cancellationToken);

        var reports = new List<MaterialConsumptionVariance>();

        foreach (var binding in bindings)
        {
            var revision = activeRevisions[binding.TaskRevisionId];
            var norm = norms[binding.NormVersionId];
            var inputVersion = materialVersions[norm.MaterialVersionId];
            var productVersion = materialVersions[norm.ProductVersionId];

            // Norm versions are frozen into task revisions; changing the latest norm must not rewrite history.
            if (inputVersion.MaterialId != binding.MaterialId
                || productVersion.MaterialId != revision.ProductId
                || norm.ProcessSegmentVersionId != revision.ProcessSegmentVersionId
                || norm.OutputUnitId != revision.UnitId)
            {
                throw new InvalidOperationException($"Consumption norm '{norm.Id}' does not match task revision '{revision.Id}'.");
            }

            var acceptedOutput = movementTotals
                .Where(total => total.TaskRevisionId == revision.Id
                    && total.MaterialId == revision.ProductId
                    && total.UnitId == norm.OutputUnitId
                    && total.Kind == MaterialMovementKind.Output)
                .Sum(total => total.Quantity);

            var consumed = movementTotals
                .Where(total => total.TaskRevisionId == revision.Id
                    && total.MaterialId == binding.MaterialId
                    && total.UnitId == norm.InputUnitId
                    && total.Kind == MaterialMovementKind.Consume)
                .Sum(total => total.Quantity);

            var returned = movementTotals
                .Where(total => total.TaskRevisionId == revision.Id
                    && total.MaterialId == binding.MaterialId
                    && total.UnitId == norm.InputUnitId
                    && total.Kind == MaterialMovementKind.Return)
                .Sum(total => total.Quantity);

            if (acceptedOutput == 0m && consumed == 0m && returned == 0m)
            {
                continue;
            }

            var expected = acceptedOutput * norm.QuantityPerOutput;
            var netConsumption = consumed - returned;
            var variance = netConsumption - expected;

            var report = new MaterialConsumptionVariance
            {
                TaskId = revision.TaskId,
                TaskRevisionId = revision.Id,
                NormVersionId = norm.Id,
                MaterialId = binding.MaterialId,
                ProductId = revision.ProductId,
                AcceptedOutput = acceptedOutput,
                QuantityPerOutput = norm.QuantityPerOutput,
                ExpectedConsumption = expected,
                ConsumedQuantity = consumed,
                ReturnedQuantity = returned,
                NetConsumption = netConsumption,
                VarianceQuantity = variance,
                VariancePercent = expected == 0m ? null : Math.Round(variance / expected * 100m, 2),
                IsOverconsumption = variance > 0m,
                InputUnitId = norm.InputUnitId,
                OutputUnitId = norm.OutputUnitId,
                AsOf = asOfUtc
            };

            reports.Add(report);
        }

        return reports.OrderBy(report => report.TaskId).ThenBy(report => report.MaterialId).ToList();
    }
}
