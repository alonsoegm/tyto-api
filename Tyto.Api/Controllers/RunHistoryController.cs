using Microsoft.AspNetCore.Mvc;
using Tyto.Api.Application.Common;
using Tyto.Api.Application.Common.Constants;
using Tyto.Api.Application.DTOs.RunHistory;
using Tyto.Api.Application.Interfaces;

namespace Tyto.Api.Controllers;

/// <summary>Provides read-only access to execution run history.</summary>
[ApiController]
[Route("api/run-history")]
public class RunHistoryController : ControllerBase
{
    private readonly IRunHistoryService _service;

    public RunHistoryController(IRunHistoryService service) => _service = service;

    /// <summary>Returns a paged list of run history entries.</summary>
    /// <param name="configurationId">Optional filter by configuration identifier.</param>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<RunHistoryResponseDto>>>> GetAll(
        [FromQuery] QueryParameters parameters, [FromQuery] Guid? configurationId, CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(parameters, configurationId, cancellationToken);

        if (result.IsFailed)
            return StatusCode(500, result.ToApiResponse());

        return Ok(result.ToApiResponse());
    }

    /// <summary>Returns a run history entry by id.</summary>
    /// <param name="id">The run history identifier.</param>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<RunHistoryResponseDto>>> GetById(
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
}
