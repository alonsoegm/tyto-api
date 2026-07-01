using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Tyto.Api.Application.Common;
using Tyto.Api.Application.Common.Constants;
using Tyto.Api.Application.DTOs.DocumentModel;
using Tyto.Api.Application.Interfaces;
using Tyto.Api.Extensions;

namespace Tyto.Api.Controllers;

/// <summary>Manages document intelligence model configurations.</summary>
[ApiController]
[Route("api/document-models")]
public class DocumentModelsController : ControllerBase
{
    private readonly IDocumentModelService _service;

    public DocumentModelsController(IDocumentModelService service) => _service = service;

    private string CurrentUser =>
        User.FindFirst(AppClaimTypes.ObjectId)?.Value ?? User.FindFirst(AppClaimTypes.Email)?.Value ?? "unknown";

    /// <summary>Returns a paged list of document models.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<DocumentModelResponseDto>>>> GetAll(
        [FromQuery] QueryParameters parameters, CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(parameters, cancellationToken);

        if (result.IsFailed)
            return StatusCode(500, result.ToApiResponse());

        return Ok(result.ToApiResponse());
    }

    /// <summary>Returns a document model by id.</summary>
    /// <param name="id">The document model identifier.</param>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<DocumentModelResponseDto>>> GetById(
        Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);

        if (result.IsFailed)
        {
            var errorCode = result.GetErrorCode();
            return errorCode == ErrorCodes.NotFound
                ? NotFound(result.ToApiResponse())
                : StatusCode(500, result.ToApiResponse());
        }

        return Ok(result.ToApiResponse());
    }

    /// <summary>Creates a new document model.</summary>
    /// <param name="dto">The document model data.</param>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<DocumentModelResponseDto>>> Create(
        [FromBody] DocumentModelCreateDto dto, CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(dto, CurrentUser, cancellationToken);

        if (result.IsFailed)
        {
            var errorCode = result.GetErrorCode();
            return errorCode switch
            {
                ErrorCodes.ValidationError => BadRequest(result.ToApiResponse()),
                ErrorCodes.Conflict => Conflict(result.ToApiResponse()),
                ErrorCodes.NotFound => NotFound(result.ToApiResponse()),
                _ => StatusCode(500, result.ToApiResponse())
            };
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.ToApiResponse());
    }

    /// <summary>Updates an existing document model.</summary>
    /// <param name="id">The document model identifier.</param>
    /// <param name="dto">The updated document model data.</param>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<DocumentModelResponseDto>>> Update(
        Guid id, [FromBody] DocumentModelUpdateDto dto, CancellationToken cancellationToken)
    {
        var result = await _service.UpdateAsync(id, dto, CurrentUser, cancellationToken);

        if (result.IsFailed)
        {
            var errorCode = result.GetErrorCode();
            return errorCode switch
            {
                ErrorCodes.NotFound => NotFound(result.ToApiResponse()),
                ErrorCodes.ValidationError => BadRequest(result.ToApiResponse()),
                ErrorCodes.Conflict => Conflict(result.ToApiResponse()),
                _ => StatusCode(500, result.ToApiResponse())
            };
        }

        return Ok(result.ToApiResponse());
    }

    /// <summary>Deletes a document model by id.</summary>
    /// <param name="id">The document model identifier.</param>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.DeleteAsync(id, CurrentUser, cancellationToken);

        if (result.IsFailed)
        {
            var errorCode = result.GetErrorCode();
            return errorCode switch
            {
                ErrorCodes.NotFound => NotFound(result.ToApiResponse()),
                ErrorCodes.Conflict => Conflict(result.ToApiResponse()),
                _ => StatusCode(500, result.ToApiResponse())
            };
        }

        return NoContent();
    }

    /// <summary>Marks a document model as the default, unsetting any previous default.</summary>
    /// <param name="id">The document model identifier.</param>
    [HttpPost("{id:guid}/set-default")]
    public async Task<ActionResult<ApiResponse<DocumentModelResponseDto>>> SetDefault(
        Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.SetDefaultAsync(id, CurrentUser, cancellationToken);

        if (result.IsFailed)
        {
            var errorCode = result.GetErrorCode();
            return errorCode switch
            {
                ErrorCodes.NotFound => NotFound(result.ToApiResponse()),
                _ => StatusCode(500, result.ToApiResponse())
            };
        }

        return Ok(result.ToApiResponse());
    }

    /// <summary>Tests a document intelligence connection without saving a record.</summary>
    /// <param name="dto">The connection values to test.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A connection test result.</returns>
    [HttpPost("test-connection")]
    [EnableRateLimiting(RateLimitingExtensions.TestConnectionPolicy)]
    public async Task<ActionResult<ApiResponse<TestDocumentModelConnectionResultDto>>> TestConnection(
        [FromBody] TestDocumentModelConnectionDto dto,
        CancellationToken cancellationToken)
    {
        var result = await _service.TestConnectionAsync(dto, cancellationToken);

        if (result.IsFailed)
        {
            var errorCode = result.GetErrorCode();
            return errorCode switch
            {
                ErrorCodes.NotFound => NotFound(result.ToApiResponse()),
                ErrorCodes.ValidationError => BadRequest(result.ToApiResponse()),
                _ => StatusCode(500, result.ToApiResponse())
            };
        }

        return Ok(result.ToApiResponse());
    }
}
