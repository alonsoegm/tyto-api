using Microsoft.AspNetCore.Mvc;
using Tyto.Api.Application.Common;
using Tyto.Api.Application.DTOs.AuditLog;
using Tyto.Api.Application.Interfaces;
using Tyto.Api.Domain.Enums;

namespace Tyto.Api.Controllers;

/// <summary>Provides read-only access to the audit log.</summary>
[ApiController]
[Route("api/audit-logs")]
public class AuditLogController : ControllerBase
{
    private readonly IAuditLogService _service;

    public AuditLogController(IAuditLogService service) => _service = service;

    /// <summary>Returns a paged list of audit log entries.</summary>
    /// <param name="entityType">Optional filter by entity type.</param>
    /// <param name="entityId">Optional filter by entity identifier.</param>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<AuditLogResponseDto>>>> GetAll(
        [FromQuery] QueryParameters parameters,
        [FromQuery] AuditEntityType? entityType,
        [FromQuery] Guid? entityId,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(parameters, entityType, entityId, cancellationToken);

        if (result.IsFailed)
            return result.ToErrorResult(this);

        return Ok(result.ToApiResponse());
    }

    /// <summary>Returns an audit log entry by id.</summary>
    /// <param name="id">The audit log entry identifier.</param>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<AuditLogResponseDto>>> GetById(
        Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);

        if (result.IsFailed)
            return result.ToErrorResult(this);

        return Ok(result.ToApiResponse());
    }
}
