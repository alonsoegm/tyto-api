using System.Text.Json.Nodes;
using Tyto.Api.Domain.Enums;

namespace Tyto.Api.Application.DTOs.DatabaseConnection;

/// <summary>
/// Connection projection returned to clients. <see cref="Config"/> is the connection-type-specific
/// payload with all secret values masked; it is null for internal connections.
/// </summary>
public record DatabaseConnectionResponseDto(
    Guid Id,
    string Name,
    string Description,
    ConnectionType ConnectionType,
    bool IsInternal,
    int ConfigurationsCount,
    DateTime? LastTestDate,
    string? LastTestStatus,
    int? LastTestStatusCode,
    string? LastTestMessage,
    JsonObject? Config,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string CreatedBy,
    string UpdatedBy
);
