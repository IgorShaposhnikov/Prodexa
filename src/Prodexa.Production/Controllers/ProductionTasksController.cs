using Microsoft.AspNetCore.Mvc;
using Prodexa.Production.Models;
using Prodexa.Production.Services;

namespace Prodexa.Production.Controllers;

/// <summary>
/// Exposes read-only access to production tasks and their plan revisions.
/// </summary>
[ApiController]
[Route("api/production/tasks")]
public sealed class ProductionTasksController(ProductionTaskService tasks) : ControllerBase
{
    /// <summary>
    /// Lists production tasks with bounded pagination.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProductionTaskSummary>>> GetTasks([FromQuery] int offset = 0, [FromQuery] int limit = 50, CancellationToken cancellationToken = default)
    {
        if (offset < 0 || limit is < 1 or > 100)
        {
            return BadRequest("Offset must be non-negative and limit must be between 1 and 100.");
        }

        var result = await tasks.GetTasksAsync(offset, limit, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets a production task and its plan revisions.
    /// </summary>
    [HttpGet("{taskId}")]
    public async Task<ActionResult<ProductionTaskDetails>> GetTask(string taskId, CancellationToken cancellationToken)
    {
        var task = await tasks.GetTaskAsync(taskId, cancellationToken);

        if (task is null)
        {
            return NotFound();
        }

        return Ok(task);
    }
}
