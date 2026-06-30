using Tyto.Api.Domain.Enums;

namespace Tyto.Api.Application.DTOs.DatabaseConnection;

public record TestDatabaseConnectionDto(
    ConnectionType ConnectionType,

    // Salesforce
    SalesforceAuthMethod? SF_AuthMethod,
    string? SF_InstanceUrl,
    string? SF_Username,
    string? SF_ConsumerKey,
    string? SF_ClientSecret,
    string? SF_ApiVersion,
    bool SF_RunAsIntegrationUser,
    SigningKeySource? SF_SigningKeySource,
    string? SF_JwtAudience,
    string? SF_PrivateKeyFile,
    string? SF_Passphrase,
    string? SF_KeyVaultUrl,
    string? SF_KeyVaultSecretName,

    // Dataverse
    DataverseAuthMethod? DV_AuthMethod,
    string? DV_EnvironmentUrl,
    string? DV_TenantId,
    string? DV_ClientId,
    string? DV_ClientSecret,
    CertificateSource? DV_CertificateSource,
    string? DV_CertificateData,
    string? DV_KeyVaultUrl,
    string? DV_KeyVaultCertificateName,
    ManagedIdentityType? DV_ManagedIdentityType,
    string? DV_UserAssignedClientId
);
