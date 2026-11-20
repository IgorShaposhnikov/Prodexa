using Microsoft.AspNetCore.Mvc;
using Prodexa.Importing.Models;
using Prodexa.Importing.Services;

namespace Prodexa.Importing.Controllers;

/// <summary>
/// Imports manufacturing records while preserving their origin and source revision.
/// </summary>
[ApiController]
[Route("api/import")]
[RequestSizeLimit(1_048_576)]
public sealed class ImportController(ManufacturingImportService importer, CsvImportParser csvParser) : ControllerBase
{
    /// <summary>
    /// Imports a JSON batch of related production entities in a single transaction.
    /// </summary>
    [HttpPost("records")]
    [Consumes("application/json")]
    public async Task<ActionResult<ImportBatchResult>> ImportRecords([FromBody] ImportBatchRequest request, CancellationToken cancellationToken)
    {
        return await ProcessAsync(request, cancellationToken);
    }

    /// <summary>
    /// Imports downtime events from CSV after the referenced equipment has been created.
    /// </summary>
    [HttpPost("downtime-csv")]
    [Consumes("text/csv")]
    public async Task<ActionResult<ImportBatchResult>> ImportDowntimeCsv([FromQuery] string sourceSystem, CancellationToken cancellationToken)
    {
        try
        {
            using var reader = new StreamReader(Request.Body);
            var csv = await reader.ReadToEndAsync(cancellationToken);
            var batch = csvParser.ParseDowntimes(sourceSystem, csv);

            return await ProcessAsync(batch, cancellationToken);
        }
        catch (ImportValidationException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    /// <summary>
    /// Imports consumption, return, and production output events from CSV.
    /// </summary>
    [HttpPost("material-movements-csv")]
    [Consumes("text/csv")]
    public async Task<ActionResult<ImportBatchResult>> ImportMaterialMovementsCsv([FromQuery] string sourceSystem, CancellationToken cancellationToken)
    {
        try
        {
            using var reader = new StreamReader(Request.Body);
            var csv = await reader.ReadToEndAsync(cancellationToken);
            var batch = csvParser.ParseMovements(sourceSystem, csv);

            return await ProcessAsync(batch, cancellationToken);
        }
        catch (ImportValidationException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    private async Task<ActionResult<ImportBatchResult>> ProcessAsync(ImportBatchRequest batch, CancellationToken cancellationToken)
    {
        try
        {
            var result = await importer.ImportAsync(batch, cancellationToken);
            return Ok(result);
        }
        catch (ImportValidationException exception)
        {
            return BadRequest(exception.Message);
        }
        catch (ImportConflictException exception)
        {
            return Conflict(exception.Message);
        }
    }
}
