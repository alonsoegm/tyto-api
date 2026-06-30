using Tyto.Api.Domain.Enums;

namespace Tyto.Api.Application.DTOs.AuditLog;

public record AuditLogResponseDto(
    Guid Id,
    AuditAction Action,
    AuditEntityType EntityType,
    Guid EntityId,
    string? EntityName,
    string? Changes,
    string PerformedBy,
    DateTime PerformedAt,
    string? IpAddress
);
