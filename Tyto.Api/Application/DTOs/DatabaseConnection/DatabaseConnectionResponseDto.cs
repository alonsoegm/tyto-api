using Tyto.Api.Domain.Enums;

namespace Tyto.Api.Application.DTOs.DatabaseConnection;

public record DatabaseConnectionResponseDto(
    Guid Id,
    string DisplayName,
    ConnectionType ConnectionType,
    int ConfigurationsCount,
    DateTime? LastTestDate,
    string? LastTestStatus,
    int? LastTestStatusCode,
    string? LastTestMessage,

    // Salesforce (non-sensitive)
    SalesforceAuthMethod? SF_AuthMethod,
    string? SF_InstanceUrl,
    string? SF_Username,
    string? SF_ConsumerKey,
    string? SF_ApiVersion,
    bool SF_RunAsIntegrationUser,
    SigningKeySource? SF_SigningKeySource,
    string? SF_JwtAudience,
    string? SF_KeyVaultUrl,
    string? SF_KeyVaultSecretName,
    string? SF_IsSandbox,
    bool SF_HasClientSecret,
    bool SF_HasPrivateKey,

    // Dataverse (non-sensitive)
    DataverseAuthMethod? DV_AuthMethod,
    string? DV_EnvironmentUrl,
    string? DV_TenantId,
    string? DV_ClientId,
    CertificateSource? DV_CertificateSource,
    string? DV_CertificateThumbprint,
    string? DV_KeyVaultUrl,
    string? DV_KeyVaultCertificateName,
    ManagedIdentityType? DV_ManagedIdentityType,
    string? DV_UserAssignedClientId,
    bool DV_HasClientSecret,

    DateTime CreatedAt,
    DateTime UpdatedAt,
    string CreatedBy,
    string UpdatedBy
);
