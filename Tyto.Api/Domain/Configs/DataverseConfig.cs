using Tyto.Api.Domain.Enums;

namespace Tyto.Api.Domain.Configs;

/// <summary>
/// Strongly-typed shape of a Dataverse connection's <see cref="Entities.DatabaseConnection.Config"/>
/// JSON payload. Secret members (<see cref="ClientSecret"/>, <see cref="CertificateData"/>) are stored
/// encrypted at rest; see the connection service for encryption and masking behavior.
/// </summary>
public class DataverseConfig
{
    public DataverseAuthMethod? AuthMethod { get; set; }
    public string? EnvironmentUrl { get; set; }
    public string? TenantId { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public CertificateSource? CertificateSource { get; set; }
    public string? CertificateData { get; set; }
    public string? CertificateThumbprint { get; set; }
    public string? KeyVaultUrl { get; set; }
    public string? KeyVaultCertificateName { get; set; }
    public ManagedIdentityType? ManagedIdentityType { get; set; }
    public string? UserAssignedClientId { get; set; }
}
