using Microsoft.AspNetCore.Mvc;
using Prodexa.Production.Models;
using Prodexa.Production.Services;

namespace Prodexa.Production.Controllers;

/// <summary>
/// Provides analytical reports about production task performance.
/// </summary>
[ApiController]
[Route("api/production")]
public sealed class ProductionAnalyticsController(ProductionOverdueService analysis) : ControllerBase
{
    /// <summary>
    /// Returns tasks overdue at the requested instant after counting accepted good output.
    /// </summary>
    [HttpGet("overdue-tasks")]
    public async Task<ActionResult<IReadOnlyList<OverdueProductionTask>>> GetOverdueTasks([FromQuery] DateTimeOffset? asOf, CancellationToken cancellationToken)
    {
        if (!asOf.HasValue)
        {
            return BadRequest("Specify the reporting instant using the asOf query parameter.");
        }

        var result = await analysis.GetOverdueTasksAsync(asOf.Value, cancellationToken);
        return Ok(result);
    }
}
