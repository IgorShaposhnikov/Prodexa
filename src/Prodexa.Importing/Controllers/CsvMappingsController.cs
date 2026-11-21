using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Prodexa.Importing.Models;
using Prodexa.Importing.Services;

namespace Prodexa.Importing.Controllers;

/// <summary>
/// Supports a CSV mapping editor through target schema discovery, mapped preview and reusable profiles.
/// </summary>
[ApiController]
[Route("api/import")]
public sealed class CsvMappingsController(CsvTargetCatalog catalog, CsvMappingService mappings) : ControllerBase
{
    /// <summary>
    /// Lists supported destination models and their bindable fields.
    /// </summary>
    [HttpGet("targets")]
    public ActionResult<IReadOnlyList<CsvTargetDefinition>> GetTargets()
    {
        return Ok(catalog.GetTargets());
    }

    /// <summary>
    /// Lists all reusable CSV mappings stored in PostgreSQL.
    /// </summary>
    [HttpGet("mappings")]
    public async Task<ActionResult<IReadOnlyList<CsvMappingDefinition>>> GetProfiles(CancellationToken cancellationToken)
    {
        return Ok(await mappings.ListAsync(cancellationToken));
    }

    /// <summary>
    /// Retrieves a mapping profile by its stable identifier.
    /// </summary>
    [HttpGet("mappings/{id}")]
    public async Task<ActionResult<CsvMappingDefinition>> GetProfile(string id, CancellationToken cancellationToken)
    {
        var profile = await mappings.GetAsync(id, cancellationToken);

        return profile is null ? NotFound() : Ok(profile);
    }

    /// <summary>
    /// Validates and stores a new source-to-target mapping profile.
    /// </summary>
    [HttpPost("mappings")]
    [Consumes("application/json")]
    public async Task<ActionResult<CsvMappingDefinition>> CreateProfile(
        [FromBody] CsvMappingDefinition definition,
        CancellationToken cancellationToken)
    {
        try
        {
            var created = await mappings.CreateAsync(definition, cancellationToken);
            return CreatedAtAction(nameof(GetProfile), new { id = created.Id }, created);
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

    /// <summary>
    /// Validates a mapping against up to twenty client-parsed CSV rows, without saving
    /// the mapping or any converted manufacturing records.
    /// </summary>
    [HttpPost("mappings/preview")]
    [Consumes("application/json")]
    [RequestSizeLimit(131_072)]
    public ActionResult<CsvMappingPreviewResult> PreviewCsv([FromBody] CsvMappingPreviewRequest request)
    {
        try
        {
            return Ok(mappings.Preview(request.Mapping, request.DatasetId, request.Rows));
        }
        catch (ImportValidationException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    /// <summary>
    /// Imports a CSV in batches of up to 500 source records using a saved mapping profile.
    /// Each committed batch is idempotent; previously committed batches remain after later errors.
    /// </summary>
    [HttpPost("mappings/{id}/import")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(80_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 80_000_000)]
    public async Task<ActionResult<CsvMappingImportResult>> ImportCsv(
        string id,
        [FromQuery] string datasetId,
        [FromForm] IFormFile file,
        CancellationToken cancellationToken)
    {
        var profile = await mappings.GetAsync(id, cancellationToken);

        if (profile is null)
        {
            return NotFound();
        }

        string? path = null;

        try
        {
            path = await CopyUploadAsync(file, cancellationToken);
            return Ok(await mappings.ImportAsync(profile, datasetId, path, cancellationToken));
        }
        catch (ImportValidationException exception)
        {
            return BadRequest(exception.Message);
        }
        catch (ImportConflictException exception)
        {
            return Conflict(exception.Message);
        }
        finally
        {
            if (path is not null)
            {
                System.IO.File.Delete(path);
            }
        }
    }

    private async Task<string> CopyUploadAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0 || file.Length > 75_000_000)
        {
            throw new ImportValidationException("Upload a CSV file between 1 byte and 75 MB.");
        }

        var path = Path.GetTempFileName();

        try
        {
            await using (var output = System.IO.File.Create(path))
            {
                await file.CopyToAsync(output, cancellationToken);
            }

            return path;
        }
        catch
        {
            System.IO.File.Delete(path);
            throw;
        }
    }
}
