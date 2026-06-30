namespace Tyto.Api.Application.Common.Constants;

/// <summary>
/// Named <see cref="System.Net.Http.IHttpClientFactory"/> clients used for outbound calls.
/// </summary>
public static class ExternalHttpClients
{
    /// <summary>
    /// Client used to test connections against external providers (Azure AI Foundry,
    /// Document Intelligence, Dataverse). Configured with a standard resilience pipeline
    /// (retry, timeout, circuit breaker).
    /// </summary>
    public const string ConnectionTest = "ConnectionTest";
}
