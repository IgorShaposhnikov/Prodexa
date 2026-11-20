using Microsoft.AspNetCore.Mvc;
using Prodexa.Materials.Models;
using Prodexa.Materials.Services;

namespace Prodexa.Materials.Controllers;

/// <summary>
/// Exposes analytical comparisons of actual material usage and assigned consumption norms.
/// </summary>
[ApiController]
[Route("api/materials")]
public sealed class MaterialAnalyticsController(MaterialConsumptionAnalysisService analysis) : ControllerBase
{
    /// <summary>
    /// Returns material consumption variances calculated from accepted output, consumption and returns.
    /// </summary>
    [HttpGet("consumption-variance")]
    public async Task<ActionResult<IReadOnlyList<MaterialConsumptionVariance>>> GetConsumptionVariance(
        [FromQuery] DateTimeOffset? asOf,
        CancellationToken cancellationToken)
    {
        if (!asOf.HasValue)
        {
            return BadRequest("Specify the reporting instant using the asOf query parameter.");
        }

        var report = await analysis.GetConsumptionVarianceAsync(asOf.Value, cancellationToken);
        return Ok(report);
    }
}
