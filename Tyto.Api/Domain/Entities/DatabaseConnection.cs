using Tyto.Api.Domain.Configs;
using Tyto.Api.Domain.Enums;

namespace Tyto.Api.Domain.Entities;

public class DatabaseConnection : BaseEntity
{
    /// <summary>Stable identifier of the seeded system-managed Tyto Internal connection.</summary>
    public static readonly Guid InternalConnectionId = new("11111111-1111-1111-1111-111111111111");

    /// <summary>Display name of the seeded system-managed Tyto Internal connection.</summary>
    public const string InternalConnectionName = "Tyto Internal";

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ConnectionType ConnectionType { get; set; }

    /// <summary>
    /// True for system-managed connections (e.g. the seeded Tyto Internal Azure SQL destination).
    /// System connections cannot be created, updated, deleted, or tested through the public API.
    /// </summary>
    public bool IsInternal { get; set; }

    /// <summary>
    /// Connection-type-specific configuration serialized as JSON. Its shape depends on
    /// <see cref="ConnectionType"/> (see <see cref="SalesforceConfig"/> / <see cref="DataverseConfig"/>).
    /// Null for internal connections, whose credentials come from application configuration.
    /// </summary>
    public string? Config { get; set; }

    // Test connection results (aligned with LanguageModel/DocumentModel pattern)
    public DateTime? LastTestDate { get; set; }
    public string? LastTestStatus { get; set; }
    public int? LastTestStatusCode { get; set; }
    public string? LastTestMessage { get; set; }

    public ICollection<Configuration> Configurations { get; set; } = new List<Configuration>();

    /// <summary>Deserializes <see cref="Config"/> as a Salesforce configuration, or null when absent.</summary>
    public SalesforceConfig? GetSalesforceConfig() => ConnectionConfigSerializer.Deserialize<SalesforceConfig>(Config);

    /// <summary>Deserializes <see cref="Config"/> as a Dataverse configuration, or null when absent.</summary>
    public DataverseConfig? GetDataverseConfig() => ConnectionConfigSerializer.Deserialize<DataverseConfig>(Config);
}
