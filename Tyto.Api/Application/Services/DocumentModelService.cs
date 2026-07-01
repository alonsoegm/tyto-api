using System.Net;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using FluentResults;
using FluentValidation;
using Mapster;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Tyto.Api.Application.Common;
using Tyto.Api.Application.Common.Constants;
using Tyto.Api.Application.Common.Errors;
using Tyto.Api.Application.Common.Validation;
using Tyto.Api.Application.DTOs.DocumentModel;
using Tyto.Api.Application.Interfaces;
using Tyto.Api.Domain.Entities;
using Tyto.Api.Domain.Enums;
using Tyto.Api.Infrastructure.Data;

namespace Tyto.Api.Application.Services;

public class DocumentModelService : IDocumentModelService
{
    private const string DefaultApiVersion = "2024-11-30";
    private const string DefaultModelId = "prebuilt-layout";
    private const string ApiKeyPlaceholder = "••••••••";
    private static readonly TokenRequestContext CognitiveServicesTokenRequest =
        new(["https://cognitiveservices.azure.com/.default"]);

    private readonly TytoDbContext _db;
    private readonly IAuditLogService _auditLog;
    private readonly IDataProtector _protector;
    private readonly IValidator<DocumentModelCreateDto> _createValidator;
    private readonly IValidator<DocumentModelUpdateDto> _updateValidator;
    private readonly IValidator<TestDocumentModelConnectionDto> _testConnectionValidator;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TokenCredential _tokenCredential;
    private readonly ILogger<DocumentModelService> _logger;

    public DocumentModelService(
        TytoDbContext db,
        IAuditLogService auditLog,
        IDataProtectionProvider dataProtection,
        IValidator<DocumentModelCreateDto> createValidator,
        IValidator<DocumentModelUpdateDto> updateValidator,
        IValidator<TestDocumentModelConnectionDto> testConnectionValidator,
        IHttpClientFactory httpClientFactory,
        ILogger<DocumentModelService> logger)
    {
        _db = db;
        _auditLog = auditLog;
        _protector = dataProtection.CreateProtector("DocumentModel.ApiKey");
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _testConnectionValidator = testConnectionValidator;
        _httpClientFactory = httpClientFactory;
        _tokenCredential = new DefaultAzureCredential();
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<PagedResult<DocumentModelResponseDto>>> GetAllAsync(QueryParameters parameters, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _db.DocumentModels.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(parameters.Search))
                query = query.Where(x => x.Name.Contains(parameters.Search) || x.Description.Contains(parameters.Search));

            var totalCount = await query.CountAsync(cancellationToken);

            var entities = await query
                .OrderBy(x => x.Name)
                .Skip((parameters.Page - 1) * parameters.PageSize)
                .Take(parameters.PageSize)
                .ToListAsync(cancellationToken);

            var items = entities.Select(MapToResponseDto).ToList();
            var result = PagedResult<DocumentModelResponseDto>.Create(items, totalCount, parameters.Page, parameters.PageSize);

            return Result.Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving document models");
            return Result.Fail<PagedResult<DocumentModelResponseDto>>(new InternalError("Failed to retrieve document models.", ex));
        }
    }

    /// <inheritdoc />
    public async Task<Result<DocumentModelResponseDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = await _db.DocumentModels.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (entity == null)
                return Result.Fail<DocumentModelResponseDto>(new NotFoundError(nameof(DocumentModel), id));

            return Result.Ok(MapToResponseDto(entity));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving document model {Id}", id);
            return Result.Fail<DocumentModelResponseDto>(new InternalError("Failed to retrieve document model.", ex));
        }
    }

    /// <inheritdoc />
    public async Task<Result<DocumentModelResponseDto>> CreateAsync(DocumentModelCreateDto dto, string performedBy, CancellationToken cancellationToken = default)
    {
        // Validate input
        var validation = await _createValidator.ValidateToResultAsync(dto, cancellationToken);
        if (validation.IsFailed)
            return Result.Fail<DocumentModelResponseDto>(validation.Errors);

        // Check for duplicates
        if (await _db.DocumentModels.AnyAsync(x => x.Name == dto.Name, cancellationToken))
            return Result.Fail<DocumentModelResponseDto>(new ConflictError($"A document model named '{dto.Name}' already exists."));

        try
        {
            // Test connection before saving
            var testDto = new TestDocumentModelConnectionDto
            {
                Endpoint = dto.Endpoint,
                AuthMethod = dto.AuthenticationMethod == AuthenticationMethod.ManagedIdentity
                    ? "MicrosoftEntraId"
                    : "ApiKey",
                ApiKey = dto.ApiKey,
                ModelId = dto.ModelId,
                ApiVersion = dto.ApiVersion
            };

            var testResult = await TestConnectionAsync(testDto, cancellationToken);
            if (testResult.IsFailed)
                return Result.Fail<DocumentModelResponseDto>(testResult.Errors);

            var entity = dto.Adapt<DocumentModel>();
            entity.CreatedBy = performedBy;
            entity.UpdatedBy = performedBy;

            // Store test result regardless of success/failure
            entity.LastTestDate = DateTime.UtcNow;
            entity.LastTestStatus = testResult.Value.IsSuccess ? "Success" : "Failed";
            entity.LastTestStatusCode = testResult.Value.StatusCode;

            if (!string.IsNullOrWhiteSpace(dto.ApiKey))
                entity.ApiKeyEncrypted = _protector.Protect(dto.ApiKey);

            // The first model of this type is always the default; otherwise honor the requested flag.
            var anyExists = await _db.DocumentModels.AnyAsync(cancellationToken);
            entity.IsDefault = !anyExists || dto.IsDefault;

            if (entity.IsDefault && anyExists)
            {
                // Unset the previous default first so the partial unique index is never violated mid-save.
                await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
                await UnsetCurrentDefaultAsync(null, cancellationToken);
                await _db.SaveChangesAsync(cancellationToken);
                _db.DocumentModels.Add(entity);
                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            else
            {
                _db.DocumentModels.Add(entity);
            }

            _logger.LogInformation(
                "Document model '{Name}' created by {User} with connection test status: {TestStatus}",
                entity.Name,
                performedBy,
                entity.LastTestStatus);

            var auditResult = _auditLog.Log(AuditAction.Create, AuditEntityType.DocumentModel, entity.Id, entity.Name, null, performedBy);
            if (auditResult.IsFailed)
                _logger.LogWarning("Failed to log audit for document model creation: {Errors}", string.Join(", ", auditResult.Errors.Select(e => e.Message)));

            return Result.Ok(MapToResponseDto(entity));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating document model '{Name}'", dto.Name);
            return Result.Fail<DocumentModelResponseDto>(new InternalError("Failed to create document model.", ex));
        }
    }

    /// <inheritdoc />
    public async Task<Result<DocumentModelResponseDto>> UpdateAsync(Guid id, DocumentModelUpdateDto dto, string performedBy, CancellationToken cancellationToken = default)
    {
        // Validate input
        var validation = await _updateValidator.ValidateToResultAsync(dto, cancellationToken);
        if (validation.IsFailed)
            return Result.Fail<DocumentModelResponseDto>(validation.Errors);

        try
        {
            var entity = await _db.DocumentModels.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (entity == null)
                return Result.Fail<DocumentModelResponseDto>(new NotFoundError(nameof(DocumentModel), id));

            // Check for duplicate name
            if (await _db.DocumentModels.AnyAsync(x => x.Name == dto.Name && x.Id != id, cancellationToken))
                return Result.Fail<DocumentModelResponseDto>(new ConflictError($"A document model named '{dto.Name}' already exists."));

            var wasDefault = entity.IsDefault;

            dto.Adapt(entity);
            entity.UpdatedBy = performedBy;

            // The default can only change by promoting another model, never by clearing the current default.
            if (wasDefault && !entity.IsDefault)
                return Result.Fail<DocumentModelResponseDto>(new ConflictError(
                    "Cannot unset the default document model. Set another model as the default instead."));

            // Only replace API key if it's not the placeholder
            if (!string.IsNullOrWhiteSpace(dto.ApiKey) && dto.ApiKey != ApiKeyPlaceholder)
            {
                entity.ApiKeyEncrypted = _protector.Protect(dto.ApiKey);
                _logger.LogInformation("API Key updated for document model '{Name}'", entity.Name);
            }

            // Unset the previous default first so the partial unique index is never violated mid-save.
            if (!wasDefault && entity.IsDefault)
            {
                await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
                await UnsetCurrentDefaultAsync(id, cancellationToken);
                await _db.SaveChangesAsync(cancellationToken);
                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }

            var auditResult = _auditLog.Log(AuditAction.Update, AuditEntityType.DocumentModel, entity.Id, entity.Name, null, performedBy);
            if (auditResult.IsFailed)
                _logger.LogWarning("Failed to log audit for document model update: {Errors}", string.Join(", ", auditResult.Errors.Select(e => e.Message)));

            return Result.Ok(MapToResponseDto(entity));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating document model {Id}", id);
            return Result.Fail<DocumentModelResponseDto>(new InternalError("Failed to update document model.", ex));
        }
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(Guid id, string performedBy, CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = await _db.DocumentModels.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (entity == null)
                return Result.Fail(new NotFoundError(nameof(DocumentModel), id));

            // Block deleting the default while other models exist, so a default always remains.
            if (entity.IsDefault && await _db.DocumentModels.AnyAsync(x => x.Id != id, cancellationToken))
                return Result.Fail(new ConflictError(
                    "Cannot delete the default document model. Set another model as the default first."));

            _db.DocumentModels.Remove(entity);

            _logger.LogInformation("Document model '{Name}' deleted by {User}", entity.Name, performedBy);

            var auditResult = _auditLog.Log(AuditAction.Delete, AuditEntityType.DocumentModel, id, entity.Name, null, performedBy);
            if (auditResult.IsFailed)
                _logger.LogWarning("Failed to log audit for document model deletion: {Errors}", string.Join(", ", auditResult.Errors.Select(e => e.Message)));

            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document model {Id}", id);
            return Result.Fail(new InternalError("Failed to delete document model.", ex));
        }
    }

    /// <inheritdoc />
    public async Task<Result<DocumentModelResponseDto>> SetDefaultAsync(Guid id, string performedBy, CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = await _db.DocumentModels.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (entity == null)
                return Result.Fail<DocumentModelResponseDto>(new NotFoundError(nameof(DocumentModel), id));

            if (entity.IsDefault)
                return Result.Ok(MapToResponseDto(entity));

            await using (var transaction = await _db.Database.BeginTransactionAsync(cancellationToken))
            {
                // Unset the previous default first so the partial unique index is never violated mid-save.
                await UnsetCurrentDefaultAsync(id, cancellationToken);
                await _db.SaveChangesAsync(cancellationToken);

                entity.IsDefault = true;
                entity.UpdatedBy = performedBy;
                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }

            _logger.LogInformation("Document model '{Name}' set as default by {User}", entity.Name, performedBy);

            var auditResult = _auditLog.Log(AuditAction.Update, AuditEntityType.DocumentModel, entity.Id, entity.Name, null, performedBy);
            if (auditResult.IsFailed)
                _logger.LogWarning("Failed to log audit for document model set-default: {Errors}", string.Join(", ", auditResult.Errors.Select(e => e.Message)));

            return Result.Ok(MapToResponseDto(entity));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting default document model {Id}", id);
            return Result.Fail<DocumentModelResponseDto>(new InternalError("Failed to set default document model.", ex));
        }
    }

    /// <summary>
    /// Clears the IsDefault flag on the current default document model, if any.
    /// Does not call SaveChanges; the caller controls persistence ordering.
    /// </summary>
    private async Task UnsetCurrentDefaultAsync(Guid? excludeId, CancellationToken cancellationToken)
    {
        var currentDefaults = await _db.DocumentModels
            .Where(x => x.IsDefault && (excludeId == null || x.Id != excludeId))
            .ToListAsync(cancellationToken);

        foreach (var model in currentDefaults)
            model.IsDefault = false;
    }

    /// <inheritdoc />
    public async Task<Result<TestDocumentModelConnectionResultDto>> TestConnectionAsync(
        TestDocumentModelConnectionDto dto,
        CancellationToken cancellationToken = default)
    {
        // Validate input
        var validation = await _testConnectionValidator.ValidateToResultAsync(dto, cancellationToken);
        if (validation.IsFailed)
            return Result.Fail<TestDocumentModelConnectionResultDto>(validation.Errors);

        var endpoint = dto.Endpoint.TrimEnd('/');
        var apiVersion = string.IsNullOrWhiteSpace(dto.ApiVersion) ? DefaultApiVersion : dto.ApiVersion.Trim();
        var modelId = string.IsNullOrWhiteSpace(dto.ModelId) ? DefaultModelId : dto.ModelId.Trim();
        var requestUrl = BuildAzureRequestUrl(endpoint, modelId, apiVersion);

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);

        if (IsMicrosoftEntraId(dto.AuthMethod))
        {
            try
            {
                var token = await _tokenCredential.GetTokenAsync(CognitiveServicesTokenRequest, cancellationToken);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not acquire Microsoft Entra ID token for document model connection test");
                return Result.Ok(new TestDocumentModelConnectionResultDto(
                    false,
                    "Failed to acquire a Microsoft Entra ID token for Document Intelligence.",
                    null));
            }
        }
        else
        {
            request.Headers.Add("Ocp-Apim-Subscription-Key", dto.ApiKey!);
        }

        var client = _httpClientFactory.CreateClient(ExternalHttpClients.ConnectionTest);
        client.Timeout = TimeSpan.FromSeconds(20);

        try
        {
            using var response = await client.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return Result.Ok(new TestDocumentModelConnectionResultDto(
                    true,
                    "Document model connection test succeeded.",
                    (int)response.StatusCode));
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var remoteMessage = TryExtractErrorMessage(body);

            return Result.Ok(new TestDocumentModelConnectionResultDto(
                false,
                string.IsNullOrWhiteSpace(remoteMessage)
                    ? $"Document model connection test failed with status {(int)response.StatusCode}."
                    : $"Document model connection test failed with status {(int)response.StatusCode}: {remoteMessage}",
                (int)response.StatusCode));
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result.Ok(new TestDocumentModelConnectionResultDto(
                false,
                "Document model connection test timed out.",
                (int)HttpStatusCode.RequestTimeout));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Document model connection test request failed for endpoint {Endpoint}", dto.Endpoint);

            return Result.Ok(new TestDocumentModelConnectionResultDto(
                false,
                $"Document model connection test failed: {ex.Message}",
                null));
        }
    }

    private static bool IsMicrosoftEntraId(string authMethod) =>
        string.Equals(authMethod, "MicrosoftEntraId", StringComparison.OrdinalIgnoreCase);

    private static string BuildAzureRequestUrl(string endpoint, string? modelId, string apiVersion)
    {
        var encodedVersion = Uri.EscapeDataString(apiVersion);

        if (string.IsNullOrWhiteSpace(modelId))
            return $"{endpoint}/documentintelligence/documentModels?api-version={encodedVersion}";

        return
            $"{endpoint}/documentintelligence/documentModels/{Uri.EscapeDataString(modelId.Trim())}?api-version={encodedVersion}";
    }

    private static string? TryExtractErrorMessage(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        try
        {
            using var document = JsonDocument.Parse(body);

            if (document.RootElement.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var messageProperty) &&
                messageProperty.ValueKind == JsonValueKind.String)
                return messageProperty.GetString();
        }
        catch (JsonException)
        {
            // Ignore parsing errors and return a generic message.
        }

        return null;
    }

    /// <summary>
    /// Maps a DocumentModel entity to a response DTO, including the API key placeholder.
    /// </summary>
    private static DocumentModelResponseDto MapToResponseDto(DocumentModel entity)
    {
        return new DocumentModelResponseDto(
            entity.Id,
            entity.Name,
            entity.Description,
            entity.Endpoint,
            entity.ModelId,
            entity.ApiVersion,
            entity.AuthenticationMethod,
            entity.ManagedIdentityType,
            entity.UserAssignedClientId,
            entity.IsActive,
            entity.IsDefault,
            string.IsNullOrWhiteSpace(entity.ApiKeyEncrypted) ? null : ApiKeyPlaceholder,
            entity.LastTestDate,
            entity.LastTestStatus,
            entity.LastTestStatusCode,
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.CreatedBy,
            entity.UpdatedBy
        );
    }
}
