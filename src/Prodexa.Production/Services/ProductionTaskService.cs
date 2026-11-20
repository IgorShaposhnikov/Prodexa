using Microsoft.EntityFrameworkCore;
using Prodexa.Persistence;
using Prodexa.Production.Models;

namespace Prodexa.Production.Services;

/// <summary>
/// Reads production tasks and their plan history from PostgreSQL.
/// </summary>
public sealed class ProductionTaskService(ProdexaDbContext context)
{
    /// <summary>
    /// Returns a deterministic page of production tasks with their revision counts.
    /// </summary>
    public async Task<IReadOnlyList<ProductionTaskSummary>> GetTasksAsync(int offset, int limit, CancellationToken cancellationToken)
    {
        return await context.PlanTasks.AsNoTracking()
            .OrderBy(task => task.Id)
            .Skip(offset)
            .Take(limit)
            .Select(task => new ProductionTaskSummary
            {
                Id = task.Id,
                RevisionCount = context.PlanTaskRevisions.Count(revision => revision.TaskId == task.Id)
            })
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Returns the complete plan history for a task, or null when it does not exist.
    /// </summary>
    public async Task<ProductionTaskDetails?> GetTaskAsync(string taskId, CancellationToken cancellationToken)
    {
        var exists = await context.PlanTasks.AsNoTracking()
            .AnyAsync(task => task.Id == taskId, cancellationToken);

        if (!exists)
        {
            return null;
        }

        var revisions = await context.PlanTaskRevisions.AsNoTracking()
            .Where(revision => revision.TaskId == taskId)
            .OrderBy(revision => revision.RevisionNumber)
            .ToListAsync(cancellationToken);

        var result = new List<ProductionTaskRevision>(revisions.Count);

        foreach (var revision in revisions)
        {
            var taskRevision = new ProductionTaskRevision
            {
                Id = revision.Id,
                RevisionNumber = revision.RevisionNumber,
                ProductId = revision.ProductId,
                ProcessSegmentVersionId = revision.ProcessSegmentVersionId,
                PlannedQuantity = revision.PlannedQuantity,
                UnitId = revision.UnitId,
                DueAt = revision.DueAt,
                Status = revision.Status.ToString(),
                EffectiveFrom = revision.EffectiveFrom,
                EffectiveTo = revision.EffectiveTo
            };

            result.Add(taskRevision);
        }

        return new ProductionTaskDetails
        {
            Id = taskId,
            Revisions = result
        };
    }
}
