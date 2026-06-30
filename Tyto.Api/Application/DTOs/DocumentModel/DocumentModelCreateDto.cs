using Tyto.Api.Domain.Enums;

namespace Tyto.Api.Application.DTOs.DocumentModel;

public record DocumentModelCreateDto(
    string Name,
    string Description,
    string Endpoint,
    string? ModelId,
    string ApiVersion,
    AuthenticationMethod AuthenticationMethod,
    string? ApiKey,
    ManagedIdentityType? ManagedIdentityType,
    string? UserAssignedClientId,
    bool IsActive,
    bool IsDefault
);
