using Tyto.Api.Domain.Enums;

namespace Tyto.Api.Application.DTOs.Configuration;

public record ConfigurationUpdateDto(
    string Name,
    string Description,
    ConfigurationStatus Status,
    ExtractionStrategy ExtractionStrategy,
    ModelSelectionMode ModelSelectionMode,
    Guid LanguageModelId,
    Guid? DocumentModelId,
    Guid DatabaseConnectionId,
    string TargetObject,
    string? SystemPrompt,
    string? UserPromptTemplate,
    int MaxTokens,
    double Temperature
);
