using Tyto.Api.Domain.Enums;

namespace Tyto.Api.Domain.Entities;

public class LanguageModel : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ServiceType ServiceType { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public string DeploymentName { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = string.Empty;
    public AuthenticationMethod AuthenticationMethod { get; set; }
    public string? ApiKeyEncrypted { get; set; }
    public ManagedIdentityType? ManagedIdentityType { get; set; }
    public string? UserAssignedClientId { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Indicates whether this is the default language model used when "Use Default" is selected.
    /// Only one language model can be the default at a time.
    /// </summary>
    public bool IsDefault { get; set; }

    // New fields for test connection results and provider-specific config
    public string? ModelName { get; set; }
    public string? ApiSurface { get; set; }
    public DateTime? LastTestDate { get; set; }
    public string? LastTestStatus { get; set; }
    public int? LastTestStatusCode { get; set; }
    public string? LastTestMessage { get; set; }

    public ICollection<Configuration> Configurations { get; set; } = new List<Configuration>();
}
