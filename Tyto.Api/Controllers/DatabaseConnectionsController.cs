using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Tyto.Api.Application.Common;
using Tyto.Api.Application.Common.Constants;
using Tyto.Api.Application.DTOs.DatabaseConnection;
using Tyto.Api.Application.Interfaces;
using Tyto.Api.Extensions;

namespace Tyto.Api.Controllers;

/// <summary>Manages CRM database connections.</summary>
[ApiController]
[Route("api/database-connections")]
public class DatabaseConnectionsController : ControllerBase
{
    private readonly IDatabaseConnectionService _service;

    public DatabaseConnectionsController(IDatabaseConnectionService service) => _service = service;

    private string CurrentUser =>
        User.FindFirst(AppClaimTypes.ObjectId)?.Value ?? User.FindFirst(AppClaimTypes.Email)?.Value ?? "unknown";

    /// <summary>Returns a paged list of database connections.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<DatabaseConnectionResponseDto>>>> GetAll(
        [FromQuery] QueryParameters parameters, CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(parameters, cancellationToken);

        if (result.IsFailed)
            return result.ToErrorResult(this);

        return Ok(result.ToApiResponse());
    }

    /// <summary>Returns a database connection by id.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<DatabaseConnectionResponseDto>>> GetById(
        Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);

        if (result.IsFailed)
            return result.ToErrorResult(this);

        return Ok(result.ToApiResponse());
    }

    /// <summary>Creates a new database connection.</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<DatabaseConnectionResponseDto>>> Create(
        [FromBody] DatabaseConnectionCreateDto dto, CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(dto, CurrentUser, cancellationToken);

        if (result.IsFailed)
            return result.ToErrorResult(this);

        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.ToApiResponse());
    }

    /// <summary>Updates an existing database connection.</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<DatabaseConnectionResponseDto>>> Update(
        Guid id, [FromBody] DatabaseConnectionUpdateDto dto, CancellationToken cancellationToken)
    {
        var result = await _service.UpdateAsync(id, dto, CurrentUser, cancellationToken);

        if (result.IsFailed)
            return result.ToErrorResult(this);

        return Ok(result.ToApiResponse());
    }

    /// <summary>Deletes a database connection by id.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.DeleteAsync(id, CurrentUser, cancellationToken);

        if (result.IsFailed)
            return result.ToErrorResult(this);

        return NoContent();
    }

    /// <summary>Tests a database connection ad-hoc from form values without saving.</summary>
    /// <param name="dto">The connection values to test.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A connection test result.</returns>
    [HttpPost("test-connection")]
    [EnableRateLimiting(RateLimitingExtensions.TestConnectionPolicy)]
    public async Task<ActionResult<ApiResponse<TestDatabaseConnectionResultDto>>> TestConnection(
        [FromBody] TestDatabaseConnectionDto dto, CancellationToken cancellationToken)
    {
        var result = await _service.TestConnectionAsync(dto, cancellationToken);

        if (result.IsFailed)
            return result.ToErrorResult(this);

        return Ok(result.ToApiResponse());
    }
}
