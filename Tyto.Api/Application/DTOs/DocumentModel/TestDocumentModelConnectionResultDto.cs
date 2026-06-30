namespace Tyto.Api.Application.DTOs.DocumentModel;

public record TestDocumentModelConnectionResultDto(
    bool IsSuccess,
    string Message,
    int? StatusCode
);
