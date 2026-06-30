using Tyto.Api.Domain.Enums;

namespace Tyto.Api.Application.DTOs.Configuration;

public record MappedFieldInlineDto(
    string FieldName,
    string DisplayLabel,
    FieldType FieldType,
    RequirementLevel RequirementLevel,
    string? ExtractionHint,
    string? DefaultValue
);

public record ConfigurationCreateDto(
    string Name,
    string Description,
    ExtractionStrategy ExtractionStrategy,
    ModelSelectionMode ModelSelectionMode,
    Guid LanguageModelId,
    Guid? DocumentModelId,
    Guid DatabaseConnectionId,
    string TargetObject,
    string? SystemPrompt,
    string? UserPromptTemplate,
    int MaxTokens,
    double Temperature,
    int MaxUploadSizeMB,
    List<string> AcceptedFileTypes,
    List<MappedFieldInlineDto> MappedFields
);
