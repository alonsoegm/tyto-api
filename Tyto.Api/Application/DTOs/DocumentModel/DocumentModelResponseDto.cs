using Tyto.Api.Domain.Enums;

namespace Tyto.Api.Application.DTOs.DocumentModel;

public record DocumentModelResponseDto(
    Guid Id,
    string Name,
    string Description,
    string Endpoint,
    string? ModelId,
    string ApiVersion,
    AuthenticationMethod AuthenticationMethod,
    ManagedIdentityType? ManagedIdentityType,
    string? UserAssignedClientId,
    bool IsActive,
    bool IsDefault,
    string? ApiKeyPlaceholder,
    DateTime? LastTestDate,
    string? LastTestStatus,
    int? LastTestStatusCode,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string CreatedBy,
    string UpdatedBy
);
