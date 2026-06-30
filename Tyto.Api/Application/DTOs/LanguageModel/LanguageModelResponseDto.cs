using Tyto.Api.Domain.Enums;

namespace Tyto.Api.Application.DTOs.LanguageModel;

public record LanguageModelResponseDto(
    Guid Id,
    string Name,
    string Description,
    ServiceType ServiceType,
    string Endpoint,
    string DeploymentName,
    string ApiVersion,
    AuthenticationMethod AuthenticationMethod,
    ManagedIdentityType? ManagedIdentityType,
    string? UserAssignedClientId,
    bool IsActive,
    bool IsDefault,
    string? ModelName,
    string? ApiSurface,
    DateTime? LastTestDate,
    string? LastTestStatus,
    int? LastTestStatusCode,
    string? LastTestMessage,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string CreatedBy,
    string UpdatedBy
);
