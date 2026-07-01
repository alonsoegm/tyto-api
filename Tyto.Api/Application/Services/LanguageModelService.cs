using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Azure;
using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;
using FluentResults;
using FluentValidation;
using Mapster;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using OpenAI.Chat;
using Tyto.Api.Application.Common;
using Tyto.Api.Application.Common.Constants;
using Tyto.Api.Application.Common.Errors;
using Tyto.Api.Application.Common.Validation;
using Tyto.Api.Application.DTOs.LanguageModel;
using Tyto.Api.Application.Interfaces;
using Tyto.Api.Domain.Entities;
using Tyto.Api.Domain.Enums;
using Tyto.Api.Infrastructure.Data;

namespace Tyto.Api.Application.Services;

public class LanguageModelService : ILanguageModelService
{
    private readonly TytoDbContext _db;
    private readonly IAuditLogService _auditLog;
    private readonly IDataProtector _protector;
    private readonly IValidator<LanguageModelCreateDto> _createValidator;
    private readonly IValidator<LanguageModelUpdateDto> _updateValidator;
    private readonly IValidator<TestLanguageModelConnectionDto> _testConnectionValidator;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TokenCredential _tokenCredential;
    private readonly ILogger<LanguageModelService> _logger;

    public LanguageModelService(
        TytoDbContext db,
        IAuditLogService auditLog,
        IDataProtectionProvider dataProtection,
        IValidator<LanguageModelCreateDto> createValidator,
        IValidator<LanguageModelUpdateDto> updateValidator,
        IValidator<TestLanguageModelConnectionDto> testConnectionValidator,
        IHttpClientFactory httpClientFactory,
        ILogger<LanguageModelService> logger)
    {
        _db = db;
        _auditLog = auditLog;
        _protector = dataProtection.CreateProtector("LanguageModel.ApiKey");
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _testConnectionValidator = testConnectionValidator;
        _httpClientFactory = httpClientFactory;
        _tokenCredential = new DefaultAzureCredential();
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<PagedResult<LanguageModelResponseDto>>> GetAllAsync(QueryParameters parameters, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _db.LanguageModels.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(parameters.Search))
                query = query.Where(x => x.Name.Contains(parameters.Search) || x.Description.Contains(parameters.Search));

            var totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .OrderBy(x => x.Name)
                .Skip((parameters.Page - 1) * parameters.PageSize)
                .Take(parameters.PageSize)
                .ProjectToType<LanguageModelResponseDto>()
                .ToListAsync(cancellationToken);

            var result = PagedResult<LanguageModelResponseDto>.Create(items, totalCount, parameters.Page, parameters.PageSize);
            return Result.Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving language models");
            return Result.Fail<PagedResult<LanguageModelResponseDto>>(new InternalError("Failed to retrieve language models.", ex));
        }
    }

    /// <inheritdoc />
    public async Task<Result<LanguageModelResponseDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = await _db.LanguageModels.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (entity == null)
                return Result.Fail<LanguageModelResponseDto>(new NotFoundError(nameof(LanguageModel), id));

            return Result.Ok(entity.Adapt<LanguageModelResponseDto>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving language model {Id}", id);
            return Result.Fail<LanguageModelResponseDto>(new InternalError("Failed to retrieve language model.", ex));
        }
    }

    /// <inheritdoc />
    public async Task<Result<LanguageModelResponseDto>> CreateAsync(LanguageModelCreateDto dto, string performedBy, CancellationToken cancellationToken = default)
    {
        // Validate input
        var validation = await _createValidator.ValidateToResultAsync(dto, cancellationToken);
        if (validation.IsFailed)
            return Result.Fail<LanguageModelResponseDto>(validation.Errors);

        // Check for duplicates
        if (await _db.LanguageModels.AnyAsync(x => x.Name == dto.Name, cancellationToken))
            return Result.Fail<LanguageModelResponseDto>(new ConflictError($"A language model named '{dto.Name}' already exists."));

        try
        {
            var entity = dto.Adapt<LanguageModel>();
            entity.CreatedBy = performedBy;
            entity.UpdatedBy = performedBy;

            if (!string.IsNullOrWhiteSpace(dto.ApiKey))
                entity.ApiKeyEncrypted = _protector.Protect(dto.ApiKey);

            // The first model of this type is always the default; otherwise honor the requested flag.
            var anyExists = await _db.LanguageModels.AnyAsync(cancellationToken);
            entity.IsDefault = !anyExists || dto.IsDefault;

            // Retrying execution strategy (EnableRetryOnFailure) requires the whole
            // transaction to run as a single retriable unit; a bare BeginTransaction throws.
            var strategy = _db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

                // Unset the previous default first so the partial unique index is never violated mid-save.
                if (entity.IsDefault && anyExists)
                {
                    await UnsetCurrentDefaultAsync(null, cancellationToken);
                    await _db.SaveChangesAsync(cancellationToken);
                }

                _db.LanguageModels.Add(entity);
                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            });

            _logger.LogInformation("Language model '{Name}' created by {User}", entity.Name, performedBy);

            // Test connection automatically after creation
            var testDto = new TestLanguageModelConnectionDto
            {
                ServiceType = entity.ServiceType.ToString(),
                Endpoint = entity.Endpoint,
                AuthMethod = entity.AuthenticationMethod == AuthenticationMethod.ApiKey ? "ApiKey" : "MicrosoftEntraId",
                ApiKey = dto.ApiKey,
                DeploymentName = entity.DeploymentName,
                ModelName = entity.ModelName,
                ApiVersion = entity.ApiVersion,
                ApiSurface = entity.ApiSurface
            };

            var testResult = await TestConnectionAsync(testDto, cancellationToken);
            if (testResult.IsSuccess)
            {
                entity.LastTestDate = DateTime.UtcNow;
                entity.LastTestStatus = testResult.Value.IsSuccess ? "Success" : "Failed";
                entity.LastTestStatusCode = testResult.Value.StatusCode;
                entity.LastTestMessage = testResult.Value.Message;
                await _db.SaveChangesAsync(cancellationToken);
            }

            var auditResult = _auditLog.Log(AuditAction.Create, AuditEntityType.LanguageModel, entity.Id, entity.Name, null, performedBy);
            if (auditResult.IsFailed)
                _logger.LogWarning("Failed to log audit for language model creation: {Errors}", string.Join(", ", auditResult.Errors.Select(e => e.Message)));

            return Result.Ok(entity.Adapt<LanguageModelResponseDto>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating language model '{Name}'", dto.Name);
            return Result.Fail<LanguageModelResponseDto>(new InternalError("Failed to create language model.", ex));
        }
    }

    /// <inheritdoc />
    public async Task<Result<LanguageModelResponseDto>> UpdateAsync(Guid id, LanguageModelUpdateDto dto, string performedBy, CancellationToken cancellationToken = default)
    {
        // Validate input
        var validation = await _updateValidator.ValidateToResultAsync(dto, cancellationToken);
        if (validation.IsFailed)
            return Result.Fail<LanguageModelResponseDto>(validation.Errors);

        try
        {
            var entity = await _db.LanguageModels.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (entity == null)
                return Result.Fail<LanguageModelResponseDto>(new NotFoundError(nameof(LanguageModel), id));

            // Check for duplicate name
            if (await _db.LanguageModels.AnyAsync(x => x.Name == dto.Name && x.Id != id, cancellationToken))
                return Result.Fail<LanguageModelResponseDto>(new ConflictError($"A language model named '{dto.Name}' already exists."));

            // Store API key if provided (for test connection)
            string? apiKeyForTest = null;
            if (!string.IsNullOrWhiteSpace(dto.ApiKey))
            {
                apiKeyForTest = dto.ApiKey;
                entity.ApiKeyEncrypted = _protector.Protect(dto.ApiKey);
            }
            else if (!string.IsNullOrWhiteSpace(entity.ApiKeyEncrypted))
            {
                // Use existing encrypted API key for test
                try
                {
                    apiKeyForTest = _protector.Unprotect(entity.ApiKeyEncrypted);
                }
                catch
                {
                    // If we can't decrypt, skip test
                    apiKeyForTest = null;
                }
            }

            var wasDefault = entity.IsDefault;

            dto.Adapt(entity);
            entity.UpdatedBy = performedBy;

            // The default can only change by promoting another model, never by clearing the current default.
            if (wasDefault && !entity.IsDefault)
                return Result.Fail<LanguageModelResponseDto>(new ConflictError(
                    "Cannot unset the default language model. Set another model as the default instead."));

            var strategy = _db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

                // Unset the previous default first so the partial unique index is never violated mid-save.
                if (!wasDefault && entity.IsDefault)
                {
                    await UnsetCurrentDefaultAsync(id, cancellationToken);
                    await _db.SaveChangesAsync(cancellationToken);
                }

                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            });

            // Test connection automatically after update
            if (!string.IsNullOrWhiteSpace(apiKeyForTest))
            {
                var testDto = new TestLanguageModelConnectionDto
                {
                    ServiceType = entity.ServiceType.ToString(),
                    Endpoint = entity.Endpoint,
                    AuthMethod = entity.AuthenticationMethod == AuthenticationMethod.ApiKey ? "ApiKey" : "MicrosoftEntraId",
                    ApiKey = apiKeyForTest,
                    DeploymentName = entity.DeploymentName,
                    ModelName = entity.ModelName,
                    ApiVersion = entity.ApiVersion,
                    ApiSurface = entity.ApiSurface
                };

                var testResult = await TestConnectionAsync(testDto, cancellationToken);
                if (testResult.IsSuccess)
                {
                    entity.LastTestDate = DateTime.UtcNow;
                    entity.LastTestStatus = testResult.Value.IsSuccess ? "Success" : "Failed";
                    entity.LastTestStatusCode = testResult.Value.StatusCode;
                    entity.LastTestMessage = testResult.Value.Message;
                    await _db.SaveChangesAsync(cancellationToken);
                }
            }

            var auditResult = _auditLog.Log(AuditAction.Update, AuditEntityType.LanguageModel, entity.Id, entity.Name, null, performedBy);
            if (auditResult.IsFailed)
                _logger.LogWarning("Failed to log audit for language model update: {Errors}", string.Join(", ", auditResult.Errors.Select(e => e.Message)));

            return Result.Ok(entity.Adapt<LanguageModelResponseDto>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating language model {Id}", id);
            return Result.Fail<LanguageModelResponseDto>(new InternalError("Failed to update language model.", ex));
        }
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(Guid id, string performedBy, CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = await _db.LanguageModels.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (entity == null)
                return Result.Fail(new NotFoundError(nameof(LanguageModel), id));

            // Block deleting the default while other models exist, so a default always remains.
            if (entity.IsDefault && await _db.LanguageModels.AnyAsync(x => x.Id != id, cancellationToken))
                return Result.Fail(new ConflictError(
                    "Cannot delete the default language model. Set another model as the default first."));

            _db.LanguageModels.Remove(entity);

            _logger.LogInformation("Language model '{Name}' deleted by {User}", entity.Name, performedBy);

            var auditResult = _auditLog.Log(AuditAction.Delete, AuditEntityType.LanguageModel, id, entity.Name, null, performedBy);
            if (auditResult.IsFailed)
                _logger.LogWarning("Failed to log audit for language model deletion: {Errors}", string.Join(", ", auditResult.Errors.Select(e => e.Message)));

            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting language model {Id}", id);
            return Result.Fail(new InternalError("Failed to delete language model.", ex));
        }
    }

    /// <inheritdoc />
    public async Task<Result<LanguageModelResponseDto>> SetDefaultAsync(Guid id, string performedBy, CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = await _db.LanguageModels.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (entity == null)
                return Result.Fail<LanguageModelResponseDto>(new NotFoundError(nameof(LanguageModel), id));

            if (entity.IsDefault)
                return Result.Ok(entity.Adapt<LanguageModelResponseDto>());

            var strategy = _db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

                // Unset the previous default first so the partial unique index is never violated mid-save.
                await UnsetCurrentDefaultAsync(id, cancellationToken);
                await _db.SaveChangesAsync(cancellationToken);

                entity.IsDefault = true;
                entity.UpdatedBy = performedBy;
                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            });

            _logger.LogInformation("Language model '{Name}' set as default by {User}", entity.Name, performedBy);

            var auditResult = _auditLog.Log(AuditAction.Update, AuditEntityType.LanguageModel, entity.Id, entity.Name, null, performedBy);
            if (auditResult.IsFailed)
                _logger.LogWarning("Failed to log audit for language model set-default: {Errors}", string.Join(", ", auditResult.Errors.Select(e => e.Message)));

            return Result.Ok(entity.Adapt<LanguageModelResponseDto>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting default language model {Id}", id);
            return Result.Fail<LanguageModelResponseDto>(new InternalError("Failed to set default language model.", ex));
        }
    }

    /// <summary>
    /// Clears the IsDefault flag on the current default language model, if any.
    /// Does not call SaveChanges; the caller controls persistence ordering.
    /// </summary>
    private async Task UnsetCurrentDefaultAsync(Guid? excludeId, CancellationToken cancellationToken)
    {
        var currentDefaults = await _db.LanguageModels
            .Where(x => x.IsDefault && (excludeId == null || x.Id != excludeId))
            .ToListAsync(cancellationToken);

        foreach (var model in currentDefaults)
            model.IsDefault = false;
    }

    /// <inheritdoc />
    public async Task<Result<TestLanguageModelConnectionResultDto>> TestConnectionAsync(
        TestLanguageModelConnectionDto dto,
        CancellationToken cancellationToken = default)
    {
        // Validate input
        var validation = await _testConnectionValidator.ValidateToResultAsync(dto, cancellationToken);
        if (validation.IsFailed)
            return Result.Fail<TestLanguageModelConnectionResultDto>(validation.Errors);

        try
        {
            return dto.ServiceType switch
            {
                "AzureOpenAI" => await TestAzureOpenAIAsync(dto, cancellationToken),
                "AzureFoundry" => await TestAzureFoundryAsync(dto, cancellationToken),
                _ => Result.Ok(new TestLanguageModelConnectionResultDto(
                    false,
                    $"Unsupported service type: {dto.ServiceType}",
                    null))
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error testing language model connection");
            return Result.Fail<TestLanguageModelConnectionResultDto>(
                new InternalError("Failed to test language model connection.", ex));
        }
    }

    private async Task<Result<TestLanguageModelConnectionResultDto>> TestAzureOpenAIAsync(
        TestLanguageModelConnectionDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            if (dto.AuthMethod == "ApiKey" && string.IsNullOrWhiteSpace(dto.ApiKey))
                return Result.Ok(new TestLanguageModelConnectionResultDto(
                    false,
                    "API Key is required for ApiKey authentication.",
                    400));

            if (string.IsNullOrWhiteSpace(dto.DeploymentName))
                return Result.Ok(new TestLanguageModelConnectionResultDto(
                    false,
                    "Deployment name is required for Azure OpenAI.",
                    400));

            // Create Azure OpenAI client with API version
            AzureOpenAIClient client;
            if (dto.AuthMethod == "ApiKey")
            {
                var apiKey = new AzureKeyCredential(dto.ApiKey!);

                // If API version is provided, try to use it (Azure AI Foundry may need specific versions)
                // If not provided or invalid, let the SDK use its default
                if (!string.IsNullOrWhiteSpace(dto.ApiVersion))
                {
                    _logger.LogInformation("Testing Azure OpenAI connection with endpoint {Endpoint}, deployment {Deployment}, API version {ApiVersion}",
                        dto.Endpoint, dto.DeploymentName, dto.ApiVersion);
                }

                client = new AzureOpenAIClient(new Uri(dto.Endpoint), apiKey);
            }
            else // MicrosoftEntraId
            {
                client = new AzureOpenAIClient(new Uri(dto.Endpoint), _tokenCredential);
            }

            // Get chat client for the deployment
            var chatClient = client.GetChatClient(dto.DeploymentName);

            // Send a minimal test message
            var messages = new[]
            {
                ChatMessage.CreateSystemMessage("You are a test assistant. Respond with 'OK' only.")
            };

            var options = new ChatCompletionOptions
            {
                MaxOutputTokenCount = 5
            };

            var response = await chatClient.CompleteChatAsync(messages, options, cancellationToken);

            if (response?.Value != null)
            {
                _logger.LogInformation("Azure OpenAI connection test successful for endpoint {Endpoint}", dto.Endpoint);
                return Result.Ok(new TestLanguageModelConnectionResultDto(
                    true,
                    $"Connection successful. Model: {response.Value.Model}",
                    200));
            }

            return Result.Ok(new TestLanguageModelConnectionResultDto(
                false,
                "Connection test returned no response.",
                null));
        }
        catch (RequestFailedException ex)
        {
            _logger.LogWarning("Azure OpenAI connection test failed: {Message} | Status: {Status} | ErrorCode: {ErrorCode}",
                ex.Message, ex.Status, ex.ErrorCode);

            var errorMessage = $"Connection failed: HTTP {ex.Status} ({ex.ErrorCode ?? "Unknown"})";
            if (!string.IsNullOrWhiteSpace(ex.Message))
            {
                errorMessage += $"\n\n{ex.Message}";
            }

            return Result.Ok(new TestLanguageModelConnectionResultDto(
                false,
                errorMessage,
                ex.Status));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing Azure OpenAI connection");
            return Result.Ok(new TestLanguageModelConnectionResultDto(
                false,
                $"Connection test error: {ex.Message}",
                null));
        }
    }

    private async Task<Result<TestLanguageModelConnectionResultDto>> TestAzureFoundryAsync(
    TestLanguageModelConnectionDto dto,
    CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dto.DeploymentName))
                return Result.Ok(new TestLanguageModelConnectionResultDto(
                    false,
                    "Deployment name is required for Azure AI Foundry.",
                    400));

            var url = $"{dto.Endpoint.TrimEnd('/')}/openai/v1/chat/completions";

            var httpClient = _httpClientFactory.CreateClient(ExternalHttpClients.ConnectionTest);

            if (dto.AuthMethod == "ApiKey")
            {
                httpClient.DefaultRequestHeaders.Add("api-key", dto.ApiKey!);
            }
            else // MicrosoftEntraId
            {
                var tokenResult = await _tokenCredential.GetTokenAsync(
                    new TokenRequestContext(
                        ["https://cognitiveservices.azure.com/.default"]),
                    cancellationToken);

                httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue(
                        "Bearer",
                        tokenResult.Token);
            }

            var requestBody = JsonSerializer.Serialize(new
            {
                model = dto.DeploymentName,
                messages = new[]
                {
                new
                {
                    role = "user",
                    content = "Respond with OK only."
                }
            }
            });

            var httpResponse = await httpClient.PostAsync(
                url,
                new StringContent(
                    requestBody,
                    Encoding.UTF8,
                    "application/json"),
                cancellationToken);

            var responseBody = await httpResponse.Content
                .ReadAsStringAsync(cancellationToken);

            var statusCode = (int)httpResponse.StatusCode;

            if (httpResponse.IsSuccessStatusCode)
            {
                var modelName = dto.DeploymentName;

                try
                {
                    using var doc = JsonDocument.Parse(responseBody);

                    if (doc.RootElement.TryGetProperty(
                            "model",
                            out var modelProp))
                    {
                        modelName = modelProp.GetString() ?? modelName;
                    }
                }
                catch
                {
                    // Model name unavailable in the response.
                }

                _logger.LogInformation(
                    "Azure AI Foundry connection test successful for endpoint {Endpoint}",
                    dto.Endpoint);

                return Result.Ok(new TestLanguageModelConnectionResultDto(
                    true,
                    $"Connection successful. Model: {modelName}",
                    200));
            }

            _logger.LogWarning(
                "Azure AI Foundry connection test failed: Status {Status}, Body {Body}",
                statusCode,
                responseBody);

            return Result.Ok(new TestLanguageModelConnectionResultDto(
                false,
                $"Connection failed: HTTP {statusCode}\n\n{responseBody}",
                statusCode));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error testing Azure AI Foundry connection");

            return Result.Ok(new TestLanguageModelConnectionResultDto(
                false,
                $"Connection test error: {ex.Message}",
                null));
        }
    }
}
