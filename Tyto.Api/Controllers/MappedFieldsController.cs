using Microsoft.AspNetCore.Mvc;
using Tyto.Api.Application.Common;
using Tyto.Api.Application.Common.Constants;
using Tyto.Api.Application.DTOs.MappedField;
using Tyto.Api.Application.Interfaces;

namespace Tyto.Api.Controllers;

/// <summary>Manages mapped fields for extraction configurations.</summary>
[ApiController]
[Route("api/mapped-fields")]
public class MappedFieldsController : ControllerBase
{
    private readonly IMappedFieldService _service;

    public MappedFieldsController(IMappedFieldService service) => _service = service;

    private string CurrentUser =>
        User.FindFirst(AppClaimTypes.ObjectId)?.Value ?? User.FindFirst(AppClaimTypes.Email)?.Value ?? "unknown";

    /// <summary>Returns a paged list of mapped fields for a configuration.</summary>
    /// <param name="configurationId">The configuration identifier to filter by.</param>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<MappedFieldResponseDto>>>> GetAll(
        [FromQuery] Guid configurationId, [FromQuery] QueryParameters parameters, CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(configurationId, parameters, cancellationToken);

        if (result.IsFailed)
            return result.ToErrorResult(this);

        return Ok(result.ToApiResponse());
    }

    /// <summary>Returns a mapped field by id.</summary>
    /// <param name="id">The mapped field identifier.</param>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<MappedFieldResponseDto>>> GetById(
        Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);

        if (result.IsFailed)
            return result.ToErrorResult(this);

        return Ok(result.ToApiResponse());
    }

    /// <summary>Creates a new mapped field.</summary>
    /// <param name="dto">The mapped field data.</param>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<MappedFieldResponseDto>>> Create(
        [FromBody] MappedFieldCreateDto dto, CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(dto, CurrentUser, cancellationToken);

        if (result.IsFailed)
            return result.ToErrorResult(this);

        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.ToApiResponse());
    }

    /// <summary>Updates an existing mapped field.</summary>
    /// <param name="id">The mapped field identifier.</param>
    /// <param name="dto">The updated mapped field data.</param>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<MappedFieldResponseDto>>> Update(
        Guid id, [FromBody] MappedFieldUpdateDto dto, CancellationToken cancellationToken)
    {
        var result = await _service.UpdateAsync(id, dto, CurrentUser, cancellationToken);

        if (result.IsFailed)
            return result.ToErrorResult(this);

        return Ok(result.ToApiResponse());
    }

    /// <summary>Deletes a mapped field by id.</summary>
    /// <param name="id">The mapped field identifier.</param>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.DeleteAsync(id, CurrentUser, cancellationToken);

        if (result.IsFailed)
            return result.ToErrorResult(this);

        return NoContent();
    }
}
