using Tyto.Api.Domain.Enums;

namespace Tyto.Api.Domain.Configs;

/// <summary>
/// Strongly-typed shape of a Salesforce connection's <see cref="Entities.DatabaseConnection.Config"/>
/// JSON payload. Secret members (<see cref="ClientSecret"/>, <see cref="PrivateKeyFile"/>,
/// <see cref="Passphrase"/>) are stored encrypted at rest; see the connection service for
/// encryption and masking behavior.
/// </summary>
public class SalesforceConfig
{
    public SalesforceAuthMethod? AuthMethod { get; set; }
    public string? InstanceUrl { get; set; }
    public string? Username { get; set; }
    public string? ConsumerKey { get; set; }
    public string? ClientSecret { get; set; }
    public string? ApiVersion { get; set; }
    public bool RunAsIntegrationUser { get; set; }
    public SigningKeySource? SigningKeySource { get; set; }
    public string? JwtAudience { get; set; }
    public string? PrivateKeyFile { get; set; }
    public string? Passphrase { get; set; }
    public string? KeyVaultUrl { get; set; }
    public string? KeyVaultSecretName { get; set; }
    public string? IsSandbox { get; set; }
}
