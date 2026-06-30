namespace Tyto.Api.Application.DTOs.LanguageModel;

/// <summary>
/// Result of a language model connection test.
/// </summary>
public record TestLanguageModelConnectionResultDto(
    bool IsSuccess,
    string Message,
    int? StatusCode
);
