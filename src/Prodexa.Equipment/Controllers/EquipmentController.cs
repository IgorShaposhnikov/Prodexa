using Microsoft.AspNetCore.Mvc;
using Prodexa.Equipment.Models;
using Prodexa.Equipment.Services;

namespace Prodexa.Equipment.Controllers;

/// <summary>
/// Exposes calculated equipment availability rather than raw downtime CRUD operations.
/// </summary>
[ApiController]
[Route("api/equipment")]
public sealed class EquipmentController(EquipmentAnalysisService analysis) : ControllerBase
{
    /// <summary>
    /// Returns total downtime and availability for an equipment position and time window.
    /// </summary>
    [HttpGet("{equipmentId}/downtime")]
    public async Task<ActionResult<EquipmentDowntimeReport>> GetDowntime(
        string equipmentId,
        [FromQuery] DateTimeOffset? periodStart,
        [FromQuery] DateTimeOffset? periodEnd,
        CancellationToken cancellationToken)
    {
        if (!periodStart.HasValue || !periodEnd.HasValue || periodStart.Value >= periodEnd.Value)
        {
            return BadRequest("Specify a valid reporting window using periodStart and periodEnd query parameters.");
        }

        var report = await analysis.GetDowntimeReportAsync(equipmentId, periodStart.Value, periodEnd.Value, cancellationToken);

        if (report is null)
        {
            return NotFound();
        }

        return Ok(report);
    }
}
