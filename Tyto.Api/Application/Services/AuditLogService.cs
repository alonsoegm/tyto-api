using Tyto.Api.Application.Common;
using Tyto.Api.Application.Common.Errors;
using Tyto.Api.Application.DTOs.AuditLog;
using Tyto.Api.Application.Interfaces;
using Tyto.Api.Domain.Entities;
using Tyto.Api.Domain.Enums;
using Tyto.Api.Infrastructure.Data;
using FluentResults;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Tyto.Api.Application.Services;

public class AuditLogService : IAuditLogService
{
    private readonly TytoDbContext _db;

    public AuditLogService(TytoDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<Result<PagedResult<AuditLogResponseDto>>> GetAllAsync(QueryParameters parameters, AuditEntityType? entityType = null, Guid? entityId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _db.AuditLogs.AsNoTracking();

            if (entityType.HasValue)
                query = query.Where(x => x.EntityType == entityType.Value);

            if (entityId.HasValue)
                query = query.Where(x => x.EntityId == entityId.Value);

            var totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .OrderByDescending(x => x.PerformedAt)
                .Skip((parameters.Page - 1) * parameters.PageSize)
                .Take(parameters.PageSize)
                .ProjectToType<AuditLogResponseDto>()
                .ToListAsync(cancellationToken);

            var pagedResult = PagedResult<AuditLogResponseDto>.Create(items, totalCount, parameters.Page, parameters.PageSize);
            return Result.Ok(pagedResult);
        }
        catch (Exception ex)
        {
            return Result.Fail(new InternalError("Failed to retrieve audit logs.", ex));
        }
    }

    /// <inheritdoc />
    public async Task<Result<AuditLogResponseDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = await _db.AuditLogs.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (entity is null)
                return Result.Fail(new NotFoundError(nameof(AuditLog), id));

            var dto = entity.Adapt<AuditLogResponseDto>();
            return Result.Ok(dto);
        }
        catch (Exception ex)
        {
            return Result.Fail(new InternalError($"Failed to retrieve audit log {id}.", ex));
        }
    }

    /// <inheritdoc />
    public Result Log(AuditAction action, AuditEntityType entityType, Guid entityId, string? entityName, string? changes, string performedBy, string? ipAddress = null)
    {
        try
        {
            var entry = new AuditLog
            {
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                EntityName = entityName,
                Changes = changes,
                PerformedBy = performedBy,
                IpAddress = ipAddress,
                PerformedAt = DateTime.UtcNow
            };

            _db.AuditLogs.Add(entry);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail(new InternalError("Failed to log audit entry.", ex));
        }
    }
}
