using FluentResults;
using Tyto.Api.Application.Common;
using Tyto.Api.Application.DTOs.AuditLog;
using Tyto.Api.Domain.Enums;

namespace Tyto.Api.Application.Interfaces;

/// <summary>
/// Service interface for logging and querying audit events.
/// </summary>
public interface IAuditLogService
{
    /// <summary>Gets a paged list of audit log entries, optionally filtered by entity type and entity id.</summary>
    Task<Result<PagedResult<AuditLogResponseDto>>> GetAllAsync(QueryParameters parameters, AuditEntityType? entityType = null, Guid? entityId = null, CancellationToken cancellationToken = default);

    /// <summary>Gets an audit log entry by its identifier. Returns NotFoundError if not found.</summary>
    Task<Result<AuditLogResponseDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Records an audit event. Changes will be committed at the end of the request.</summary>
    Result Log(AuditAction action, AuditEntityType entityType, Guid entityId, string? entityName, string? changes, string performedBy, string? ipAddress = null);
}
