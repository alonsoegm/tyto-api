using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Tyto.Api.Application.Common.Constants;
using Tyto.Api.Application.Common.Errors;
using Tyto.Api.Application.DTOs.Metadata;
using Tyto.Api.Application.Interfaces;
using Tyto.Api.Domain.Entities;
using Tyto.Api.Domain.Enums;
using FluentResults;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Identity.Client;

namespace Tyto.Api.Application.Services.Metadata;

/// <summary>
/// Reads metadata from the Dataverse Web API (EntityDefinitions). MVP supports ClientSecret
/// authentication only; other auth methods return a clear failure. Token acquisition mirrors the
/// existing test-connection flow in <c>DatabaseConnectionService</c> but is self-contained here.
/// </summary>
public class DataverseMetadataProvider : IMetadataProvider
{
    private const string DataverseApiPath = "api/data/v9.2";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    // Dataverse logical names are limited to letters, digits and underscores. Validating against
    // this set also prevents OData single-quote injection in the EntityDefinitions key segment.
    private static readonly Regex LogicalNameRegex = new("^[a-zA-Z0-9_]+$", RegexOptions.Compiled);

    private readonly IDataProtector _protector;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DataverseMetadataProvider> _logger;

    public DataverseMetadataProvider(
        IDataProtectionProvider dataProtection,
        IHttpClientFactory httpClientFactory,
        ILogger<DataverseMetadataProvider> logger)
    {
        // Same purpose string used to encrypt the secret in DatabaseConnectionService.
        _protector = dataProtection.CreateProtector("DatabaseConnection.Dataverse");
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public ConnectionType SupportedType => ConnectionType.MsDataverse;

    public async Task<Result<List<EntityDto>>> GetEntitiesAsync(DatabaseConnection connection, CancellationToken cancellationToken = default)
    {
        var tokenResult = await AcquireTokenAsync(connection, cancellationToken);
        if (tokenResult.IsFailed)
            return Result.Fail<List<EntityDto>>(tokenResult.Errors);

        var dataResult = await GetODataValueAsync<EntityDefinitionResponse>(
            connection, tokenResult.Value,
            "EntityDefinitions?$select=LogicalName,DisplayName,IsCustomEntity&$filter=IsIntersect eq false",
            cancellationToken);
        if (dataResult.IsFailed)
            return Result.Fail<List<EntityDto>>(dataResult.Errors);

        // Only custom entities are relevant as mapping targets. Dataverse metadata queries do not
        // reliably honor $filter on EntityDefinitions (the filter is often ignored and all tables
        // are returned), so we filter on the IsCustomEntity flag here instead.
        var entities = dataResult.Value
            .Where(e => e.IsCustomEntity)
            .Select(e => new EntityDto(e.LogicalName, ResolveLabel(e.DisplayName, e.LogicalName)))
            .OrderBy(e => e.Name)
            .ToList();

        return Result.Ok(entities);
    }

    public async Task<Result<List<FieldDto>>> GetFieldsAsync(DatabaseConnection connection, string entityId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(entityId) || !LogicalNameRegex.IsMatch(entityId))
            return Result.Fail<List<FieldDto>>(new ValidationError("A valid entity logical name is required."));

        var tokenResult = await AcquireTokenAsync(connection, cancellationToken);
        if (tokenResult.IsFailed)
            return Result.Fail<List<FieldDto>>(tokenResult.Errors);

        var relativeUrl = $"EntityDefinitions(LogicalName='{entityId}')/Attributes?$select=LogicalName,DisplayName,AttributeType";
        var dataResult = await GetODataValueAsync<AttributeMetadataResponse>(
            connection, tokenResult.Value, relativeUrl, cancellationToken);
        if (dataResult.IsFailed)
            return Result.Fail<List<FieldDto>>(dataResult.Errors);

        var fields = dataResult.Value
            .Select(a => new FieldDto(a.LogicalName, ResolveLabel(a.DisplayName, a.LogicalName), a.AttributeType ?? "Unknown"))
            .OrderBy(f => f.Name)
            .ToList();

        return Result.Ok(fields);
    }

    /// <summary>
    /// Acquires a Dataverse access token for the connection. Marked virtual so tests can bypass the
    /// MSAL network call and exercise the HTTP/mapping logic in isolation.
    /// </summary>
    protected virtual async Task<Result<string>> AcquireTokenAsync(DatabaseConnection connection, CancellationToken cancellationToken)
    {
        if (connection.DV_AuthMethod != DataverseAuthMethod.ClientSecret)
            return Result.Fail<string>(new ValidationError(
                $"Dataverse metadata is only supported with ClientSecret authentication. '{connection.DV_AuthMethod}' is not yet implemented."));

        if (string.IsNullOrWhiteSpace(connection.DV_EnvironmentUrl)
            || string.IsNullOrWhiteSpace(connection.DV_TenantId)
            || string.IsNullOrWhiteSpace(connection.DV_ClientId))
            return Result.Fail<string>(new ValidationError(
                "The Dataverse connection is missing required fields (environment URL, tenant ID, or client ID)."));

        var secret = TryDecrypt(connection.DV_ClientSecret);
        if (string.IsNullOrWhiteSpace(secret))
            return Result.Fail<string>(new InternalError("Unable to read the stored Dataverse client secret."));

        try
        {
            var app = ConfidentialClientApplicationBuilder
                .Create(connection.DV_ClientId)
                .WithClientSecret(secret)
                .WithAuthority($"https://login.microsoftonline.com/{connection.DV_TenantId}")
                .Build();

            var scopes = new[] { $"{connection.DV_EnvironmentUrl}/.default" };
            var authResult = await app.AcquireTokenForClient(scopes).ExecuteAsync(cancellationToken);

            return Result.Ok(authResult.AccessToken);
        }
        catch (MsalException ex)
        {
            _logger.LogWarning(ex, "Failed to acquire Dataverse token for metadata request");
            return Result.Fail<string>(new InternalError($"Dataverse authentication failed: {ex.Message}", ex));
        }
    }

    private async Task<Result<List<T>>> GetODataValueAsync<T>(
        DatabaseConnection connection,
        string accessToken,
        string relativeUrl,
        CancellationToken cancellationToken)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient(ExternalHttpClients.ConnectionTest);
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var requestUrl = $"{connection.DV_EnvironmentUrl!.TrimEnd('/')}/{DataverseApiPath}/{relativeUrl}";
            var response = await httpClient.GetAsync(requestUrl, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Dataverse metadata request failed with status {StatusCode}: {Error}",
                    (int)response.StatusCode, errorContent);
                return Result.Fail<List<T>>(new InternalError(
                    $"Dataverse metadata request failed with HTTP {(int)response.StatusCode}."));
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var parsed = JsonSerializer.Deserialize<ODataValueResponse<T>>(content, JsonOptions);
            return Result.Ok(parsed?.Value ?? new List<T>());
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Dataverse metadata request failed");
            return Result.Fail<List<T>>(new InternalError($"Dataverse metadata request failed: {ex.Message}", ex));
        }
    }

    private string? TryDecrypt(string? encrypted)
    {
        if (string.IsNullOrWhiteSpace(encrypted)) return null;
        try
        {
            return _protector.Unprotect(encrypted);
        }
        catch
        {
            return null;
        }
    }

    private static string ResolveLabel(DataverseLabel? displayName, string fallback)
    {
        var label = displayName?.UserLocalizedLabel?.Label;
        return string.IsNullOrWhiteSpace(label) ? fallback : label;
    }

    private sealed class ODataValueResponse<T>
    {
        [JsonPropertyName("value")]
        public List<T> Value { get; set; } = new();
    }

    private sealed class EntityDefinitionResponse
    {
        public string LogicalName { get; set; } = string.Empty;
        public bool IsCustomEntity { get; set; }
        public DataverseLabel? DisplayName { get; set; }
    }

    private sealed class AttributeMetadataResponse
    {
        public string LogicalName { get; set; } = string.Empty;
        public string? AttributeType { get; set; }
        public DataverseLabel? DisplayName { get; set; }
    }

    private sealed class DataverseLabel
    {
        public LocalizedLabel? UserLocalizedLabel { get; set; }
    }

    private sealed class LocalizedLabel
    {
        public string? Label { get; set; }
    }
}
