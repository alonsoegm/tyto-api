using Tyto.Api.Domain.Enums;

namespace Tyto.Api.Domain.Entities;

public class DocumentModel : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string? ModelId { get; set; }
    public string ApiVersion { get; set; } = string.Empty;
    public AuthenticationMethod AuthenticationMethod { get; set; }
    public string? ApiKeyEncrypted { get; set; }
    public ManagedIdentityType? ManagedIdentityType { get; set; }
    public string? UserAssignedClientId { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Indicates whether this is the default document intelligence model used when "Use Default" is selected.
    /// Only one document model can be the default at a time.
    /// </summary>
    public bool IsDefault { get; set; }

    public DateTime? LastTestDate { get; set; }
    public string? LastTestStatus { get; set; }
    public int? LastTestStatusCode { get; set; }
    public string? LastTestMessage { get; set; }

    public ICollection<Configuration> Configurations { get; set; } = new List<Configuration>();
}
