namespace Tyto.Api.Application.DTOs.DocumentModel;

public record TestDocumentModelConnectionDto
{
    public required string Endpoint { get; init; }
    public required string AuthMethod { get; init; }
    public string? ApiKey { get; init; }
    public string? ModelId { get; init; }
    public string? ApiVersion { get; init; }
}
