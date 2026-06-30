namespace Tyto.Api.Application.DTOs.LanguageModel;

/// <summary>
/// DTO for testing a language model connection without persisting to database.
/// Supports Azure-deployed models: AzureOpenAI (classic) and AzureFoundry (serverless/managed compute).
/// </summary>
public record TestLanguageModelConnectionDto
{
    /// <summary>Service type: "AzureOpenAI" or "AzureFoundry".</summary>
    public required string ServiceType { get; init; }

    /// <summary>API endpoint URL.</summary>
    public required string Endpoint { get; init; }

    /// <summary>Authentication method: "ApiKey" or "MicrosoftEntraId".</summary>
    public required string AuthMethod { get; init; }

    /// <summary>API key for authentication (required when AuthMethod is "ApiKey").</summary>
    public string? ApiKey { get; init; }

    /// <summary>Deployment name (required for AzureOpenAI; optional for AzureFoundry managed compute).</summary>
    public string? DeploymentName { get; init; }

    /// <summary>Model name (optional; informational for AzureFoundry endpoints).</summary>
    public string? ModelName { get; init; }

    /// <summary>API version string (Azure OpenAI only).</summary>
    public string? ApiVersion { get; init; }

    /// <summary>API surface type: "chat", "completions", or "embeddings".</summary>
    public string? ApiSurface { get; init; }
}
