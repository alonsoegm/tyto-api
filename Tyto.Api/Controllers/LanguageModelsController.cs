using Tyto.Api.Application.Common;
using Tyto.Api.Application.Common.Constants;
using Tyto.Api.Application.DTOs.LanguageModel;
using Tyto.Api.Application.Interfaces;
using Tyto.Api.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Tyto.Api.Controllers;

/// <summary>Manages language model configurations.</summary>
[ApiController]
[Route("api/language-models")]
public class LanguageModelsController : ControllerBase
{
    private readonly ILanguageModelService _service;

    public LanguageModelsController(ILanguageModelService service) => _service = service;

    private string CurrentUser =>
        User.FindFirst(AppClaimTypes.ObjectId)?.Value ?? User.FindFirst(AppClaimTypes.Email)?.Value ?? "unknown";

    /// <summary>Returns a paged list of language models.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<LanguageModelResponseDto>>>> GetAll(
        [FromQuery] QueryParameters parameters, CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(parameters, cancellationToken);

        if (result.IsFailed)
            return result.ToErrorResult(this);

        return Ok(result.ToApiResponse());
    }

    /// <summary>Returns a language model by id.</summary>
    /// <param name="id">The language model identifier.</param>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<LanguageModelResponseDto>>> GetById(
        Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);

        if (result.IsFailed)
            return result.ToErrorResult(this);

        return Ok(result.ToApiResponse());
    }

    /// <summary>Creates a new language model.</summary>
    /// <param name="dto">The language model data.</param>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<LanguageModelResponseDto>>> Create(
        [FromBody] LanguageModelCreateDto dto, CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(dto, CurrentUser, cancellationToken);

        if (result.IsFailed)
            return result.ToErrorResult(this);

        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.ToApiResponse());
    }

    /// <summary>Updates an existing language model.</summary>
    /// <param name="id">The language model identifier.</param>
    /// <param name="dto">The updated language model data.</param>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<LanguageModelResponseDto>>> Update(
        Guid id, [FromBody] LanguageModelUpdateDto dto, CancellationToken cancellationToken)
    {
        var result = await _service.UpdateAsync(id, dto, CurrentUser, cancellationToken);

        if (result.IsFailed)
            return result.ToErrorResult(this);

        return Ok(result.ToApiResponse());
    }

    /// <summary>Deletes a language model by id.</summary>
    /// <param name="id">The language model identifier.</param>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.DeleteAsync(id, CurrentUser, cancellationToken);

        if (result.IsFailed)
            return result.ToErrorResult(this);

        return NoContent();
    }

    /// <summary>Marks a language model as the default, unsetting any previous default.</summary>
    /// <param name="id">The language model identifier.</param>
    [HttpPost("{id:guid}/set-default")]
    public async Task<ActionResult<ApiResponse<LanguageModelResponseDto>>> SetDefault(
        Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.SetDefaultAsync(id, CurrentUser, cancellationToken);

        if (result.IsFailed)
            return result.ToErrorResult(this);

        return Ok(result.ToApiResponse());
    }

    /// <summary>Tests a language model connection without saving a record.</summary>
    /// <param name="dto">The connection values to test.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A connection test result.</returns>
    [HttpPost("test-connection")]
    [EnableRateLimiting(RateLimitingExtensions.TestConnectionPolicy)]
    public async Task<ActionResult<ApiResponse<TestLanguageModelConnectionResultDto>>> TestConnection(
        [FromBody] TestLanguageModelConnectionDto dto,
        CancellationToken cancellationToken)
    {
        var result = await _service.TestConnectionAsync(dto, cancellationToken);

        if (result.IsFailed)
            return result.ToErrorResult(this);

        return Ok(result.ToApiResponse());
    }
}
