using Tyto.Api.Domain.Enums;

namespace Tyto.Api.Application.DTOs.Configuration;

public record ConfigurationResponseDto(
    Guid Id,
    string Name,
    string Description,
    ConfigurationStatus Status,
    ExtractionStrategy ExtractionStrategy,
    ModelSelectionMode ModelSelectionMode,
    Guid LanguageModelId,
    string LanguageModelName,
    Guid? DocumentModelId,
    string? DocumentModelName,
    Guid DatabaseConnectionId,
    string DatabaseConnectionName,
    string TargetObject,
    string? SystemPrompt,
    string? UserPromptTemplate,
    int MaxTokens,
    double Temperature,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string CreatedBy,
    string UpdatedBy
);
