using Tyto.Api.Domain.Enums;

namespace Tyto.Api.Domain.Entities;

public class DatabaseConnection : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ConnectionType ConnectionType { get; set; }

    // Test connection results (aligned with LanguageModel/DocumentModel pattern)
    public DateTime? LastTestDate { get; set; }
    public string? LastTestStatus { get; set; }
    public int? LastTestStatusCode { get; set; }
    public string? LastTestMessage { get; set; }

    // Salesforce fields
    public SalesforceAuthMethod? SF_AuthMethod { get; set; }
    public string? SF_InstanceUrl { get; set; }
    public string? SF_Username { get; set; }
    public string? SF_ConsumerKey { get; set; }
    public string? SF_ClientSecret { get; set; }
    public string? SF_ApiVersion { get; set; }
    public bool SF_RunAsIntegrationUser { get; set; }
    public SigningKeySource? SF_SigningKeySource { get; set; }
    public string? SF_JwtAudience { get; set; }
    public string? SF_PrivateKeyFile { get; set; }
    public string? SF_Passphrase { get; set; }
    public string? SF_KeyVaultUrl { get; set; }
    public string? SF_KeyVaultSecretName { get; set; }
    public string? SF_IsSandbox { get; set; }

    // Dataverse fields
    public DataverseAuthMethod? DV_AuthMethod { get; set; }
    public string? DV_EnvironmentUrl { get; set; }
    public string? DV_TenantId { get; set; }
    public string? DV_ClientId { get; set; }
    public string? DV_ClientSecret { get; set; }
    public CertificateSource? DV_CertificateSource { get; set; }
    public string? DV_CertificateData { get; set; }
    public string? DV_CertificateThumbprint { get; set; }
    public string? DV_KeyVaultUrl { get; set; }
    public string? DV_KeyVaultCertificateName { get; set; }
    public ManagedIdentityType? DV_ManagedIdentityType { get; set; }
    public string? DV_UserAssignedClientId { get; set; }

    public ICollection<Configuration> Configurations { get; set; } = new List<Configuration>();
}
