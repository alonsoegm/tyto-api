using Tyto.Api.Domain.Enums;

namespace Tyto.Api.Application.DTOs.LanguageModel;

public record LanguageModelUpdateDto(
    string Name,
    string Description,
    ServiceType ServiceType,
    string Endpoint,
    string DeploymentName,
    string ApiVersion,
    AuthenticationMethod AuthenticationMethod,
    string? ApiKey,
    ManagedIdentityType? ManagedIdentityType,
    string? UserAssignedClientId,
    bool IsActive,
    bool IsDefault,
    string? ModelName,
    string? ApiSurface
);
