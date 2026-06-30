using Tyto.Api.Domain.Enums;

namespace Tyto.Api.Domain.Entities;

public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public AuditAction Action { get; set; }
    public AuditEntityType EntityType { get; set; }
    public Guid EntityId { get; set; }
    public string? EntityName { get; set; }
    public string? Changes { get; set; }
    public string PerformedBy { get; set; } = string.Empty;
    public DateTime PerformedAt { get; set; } = DateTime.UtcNow;
    public string? IpAddress { get; set; }
}
