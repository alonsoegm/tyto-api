using Tyto.Api.Domain.Enums;

namespace Tyto.Api.Domain.Entities;

public class Configuration : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ConfigurationStatus Status { get; set; } = ConfigurationStatus.Draft;
    public ExtractionStrategy ExtractionStrategy { get; set; }
    public ModelSelectionMode ModelSelectionMode { get; set; }

    public Guid LanguageModelId { get; set; }
    public LanguageModel LanguageModel { get; set; } = null!;

    public Guid? DocumentModelId { get; set; }
    public DocumentModel? DocumentModel { get; set; }

    public Guid DatabaseConnectionId { get; set; }
    public DatabaseConnection DatabaseConnection { get; set; } = null!;

    public string TargetObject { get; set; } = string.Empty;
    public string? SystemPrompt { get; set; }
    public string? UserPromptTemplate { get; set; }
    public int MaxTokens { get; set; } = 4096;
    public double Temperature { get; set; } = 0.0;
    public int MaxUploadSizeMB { get; set; } = 25;
    public string AcceptedFileTypes { get; set; } = string.Empty;

    public ICollection<MappedField> MappedFields { get; set; } = new List<MappedField>();
    public ICollection<RunHistory> RunHistories { get; set; } = new List<RunHistory>();
}
