using Microsoft.AspNetCore.Mvc;
using Tyto.Api.Application.Common;
using Tyto.Api.Application.Common.Constants;
using Tyto.Api.Application.DTOs.Configuration;
using Tyto.Api.Application.DTOs.Extraction;
using Tyto.Api.Application.Interfaces;

namespace Tyto.Api.Controllers;

/// <summary>Manages extraction pipeline configurations.</summary>
[ApiController]
[Route("api/configurations")]
public class ConfigurationsController : ControllerBase
{
    private readonly IConfigurationService _service;
    private readonly IExtractionService _extractionService;

    public ConfigurationsController(IConfigurationService service, IExtractionService extractionService)
    {
        _service = service;
        _extractionService = extractionService;
    }

    private string CurrentUser =>
        User.FindFirst(AppClaimTypes.ObjectId)?.Value ?? User.FindFirst(AppClaimTypes.Email)?.Value ?? "unknown";

    /// <summary>Returns a paged list of configurations.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<ConfigurationResponseDto>>>> GetAll(
        [FromQuery] QueryParameters parameters, CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(parameters, cancellationToken);

        if (result.IsFailed)
            return result.ToErrorResult(this);

        return Ok(result.ToApiResponse());
    }

    /// <summary>Returns a configuration by id.</summary>
    /// <param name="id">The configuration identifier.</param>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ConfigurationResponseDto>>> GetById(
        Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);

        if (result.IsFailed)
            return result.ToErrorResult(this);

        return Ok(result.ToApiResponse());
    }

    /// <summary>Creates a new configuration.</summary>
    /// <param name="dto">The configuration data.</param>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<ConfigurationResponseDto>>> Create(
        [FromBody] ConfigurationCreateDto dto, CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(dto, CurrentUser, cancellationToken);

        if (result.IsFailed)
            return result.ToErrorResult(this);

        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.ToApiResponse());
    }

    /// <summary>Updates an existing configuration.</summary>
    /// <param name="id">The configuration identifier.</param>
    /// <param name="dto">The updated configuration data.</param>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ConfigurationResponseDto>>> Update(
        Guid id, [FromBody] ConfigurationUpdateDto dto, CancellationToken cancellationToken)
    {
        var result = await _service.UpdateAsync(id, dto, CurrentUser, cancellationToken);

        if (result.IsFailed)
            return result.ToErrorResult(this);

        return Ok(result.ToApiResponse());
    }

    /// <summary>Deletes a configuration by id.</summary>
    /// <param name="id">The configuration identifier.</param>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.DeleteAsync(id, CurrentUser, cancellationToken);

        if (result.IsFailed)
            return result.ToErrorResult(this);

        return NoContent();
    }

    /// <summary>
    /// Runs the configuration's extraction pipeline against an uploaded document and
    /// returns the extracted JSON. The MVP does not write to the destination database.
    /// </summary>
    /// <param name="id">The configuration identifier.</param>
    /// <param name="file">The document to extract from (PDF, DOCX, or TXT).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost("{id:guid}/extract")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ApiResponse<ExtractionResultDto>>> Extract(
        Guid id, IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse<ExtractionResultDto>.Fail(
                ErrorCodes.ValidationError, "A non-empty document file is required."));

        using var memory = new MemoryStream();
        await file.CopyToAsync(memory, cancellationToken);
        var input = new ExtractionFileInput(file.FileName, file.Length, memory.ToArray());

        var result = await _extractionService.ExtractAsync(id, input, CurrentUser, cancellationToken);

        if (result.IsFailed)
            return result.ToErrorResult(this);

        return Ok(result.ToApiResponse());
    }
}
