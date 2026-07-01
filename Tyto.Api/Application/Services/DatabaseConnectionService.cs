using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentResults;
using FluentValidation;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Client;
using Tyto.Api.Application.Common;
using Tyto.Api.Application.Common.Constants;
using Tyto.Api.Application.Common.Errors;
using Tyto.Api.Application.Common.Validation;
using Tyto.Api.Application.DTOs.DatabaseConnection;
using Tyto.Api.Application.Interfaces;
using Tyto.Api.Domain.Configs;
using Tyto.Api.Domain.Entities;
using Tyto.Api.Domain.Enums;
using Tyto.Api.Infrastructure.Data;

namespace Tyto.Api.Application.Services;

public class DatabaseConnectionService : IDatabaseConnectionService
{
    private const string SecretPlaceholder = "••••••••••••••";

    private readonly TytoDbContext _db;
    private readonly IAuditLogService _auditLog;
    private readonly IDataProtector _sfProtector;
    private readonly IDataProtector _dvProtector;
    private readonly IValidator<DatabaseConnectionCreateDto> _createValidator;
    private readonly IValidator<DatabaseConnectionUpdateDto> _updateValidator;
    private readonly IValidator<TestDatabaseConnectionDto> _testValidator;
    private readonly IValidator<SalesforceConfig> _salesforceConfigValidator;
    private readonly IValidator<DataverseConfig> _dataverseConfigValidator;
    private readonly ILogger<DatabaseConnectionService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public DatabaseConnectionService(
        TytoDbContext db,
        IAuditLogService auditLog,
        IDataProtectionProvider dataProtection,
        IValidator<DatabaseConnectionCreateDto> createValidator,
        IValidator<DatabaseConnectionUpdateDto> updateValidator,
        IValidator<TestDatabaseConnectionDto> testValidator,
        IValidator<SalesforceConfig> salesforceConfigValidator,
        IValidator<DataverseConfig> dataverseConfigValidator,
        ILogger<DatabaseConnectionService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _auditLog = auditLog;
        _sfProtector = dataProtection.CreateProtector("DatabaseConnection.Salesforce");
        _dvProtector = dataProtection.CreateProtector("DatabaseConnection.Dataverse");
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _testValidator = testValidator;
        _salesforceConfigValidator = salesforceConfigValidator;
        _dataverseConfigValidator = dataverseConfigValidator;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc />
    public async Task<Result<PagedResult<DatabaseConnectionResponseDto>>> GetAllAsync(QueryParameters parameters, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _db.DatabaseConnections
                .Include(x => x.Configurations)
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(parameters.Search))
                query = query.Where(x => x.Name.Contains(parameters.Search) || x.Description.Contains(parameters.Search));

            var totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .OrderBy(x => x.Name)
                .Skip((parameters.Page - 1) * parameters.PageSize)
                .Take(parameters.PageSize)
                .ToListAsync(cancellationToken);

            var dtos = items.Select(MapToResponseDto).ToList();
            var pagedResult = PagedResult<DatabaseConnectionResponseDto>.Create(dtos, totalCount, parameters.Page, parameters.PageSize);
            return Result.Ok(pagedResult);
        }
        catch (Exception ex)
        {
            return Result.Fail(new InternalError("Failed to retrieve database connections.", ex));
        }
    }

    /// <inheritdoc />
    public async Task<Result<DatabaseConnectionResponseDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = await _db.DatabaseConnections
                .Include(x => x.Configurations)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (entity is null)
                return Result.Fail(new NotFoundError(nameof(DatabaseConnection), id));

            return Result.Ok(MapToResponseDto(entity));
        }
        catch (Exception ex)
        {
            return Result.Fail(new InternalError($"Failed to retrieve database connection {id}.", ex));
        }
    }

    /// <inheritdoc />
    public async Task<Result<DatabaseConnectionResponseDto>> CreateAsync(DatabaseConnectionCreateDto dto, string performedBy, CancellationToken cancellationToken = default)
    {
        var validation = await _createValidator.ValidateToResultAsync(dto, cancellationToken);
        if (validation.IsFailed)
            return Result.Fail<DatabaseConnectionResponseDto>(validation.Errors);

        // Validate the type-specific payload and encrypt its secrets before persisting.
        var configResult = await PrepareStoredConfigAsync(dto.ConnectionType, dto.Config, existing: null, cancellationToken);
        if (configResult.IsFailed)
            return Result.Fail<DatabaseConnectionResponseDto>(configResult.Errors);

        try
        {
            if (await _db.DatabaseConnections.AnyAsync(x => x.Name == dto.Name, cancellationToken))
                return Result.Fail<DatabaseConnectionResponseDto>(new ConflictError($"A database connection named '{dto.Name}' already exists."));

            var entity = new DatabaseConnection
            {
                Name = dto.Name,
                Description = dto.Description,
                ConnectionType = dto.ConnectionType,
                IsInternal = false,
                Config = configResult.Value,
                CreatedBy = performedBy,
                UpdatedBy = performedBy
            };

            _db.DatabaseConnections.Add(entity);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Database connection '{Name}' created by {User}", entity.Name, performedBy);

            await ApplyAutoTestAsync(entity, cancellationToken);

            var auditResult = _auditLog.Log(AuditAction.Create, AuditEntityType.DatabaseConnection, entity.Id, entity.Name, null, performedBy);
            if (auditResult.IsFailed)
                _logger.LogWarning("Failed to log audit entry for database connection creation: {Errors}", string.Join(", ", auditResult.Errors));

            return Result.Ok(MapToResponseDto(entity));
        }
        catch (Exception ex)
        {
            return Result.Fail(new InternalError("Failed to create database connection.", ex));
        }
    }

    /// <inheritdoc />
    public async Task<Result<DatabaseConnectionResponseDto>> UpdateAsync(Guid id, DatabaseConnectionUpdateDto dto, string performedBy, CancellationToken cancellationToken = default)
    {
        DatabaseConnection? entity;
        try
        {
            entity = await _db.DatabaseConnections
                .Include(x => x.Configurations)
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Fail(new InternalError($"Failed to update database connection {id}.", ex));
        }

        if (entity is null)
            return Result.Fail<DatabaseConnectionResponseDto>(new NotFoundError(nameof(DatabaseConnection), id));

        if (entity.IsInternal)
            return Result.Fail<DatabaseConnectionResponseDto>(
                new ForbiddenError("The Tyto Internal connection is system-managed and cannot be updated."));

        var validation = await _updateValidator.ValidateToResultAsync(dto, cancellationToken);
        if (validation.IsFailed)
            return Result.Fail<DatabaseConnectionResponseDto>(validation.Errors);

        // Preserve secrets not re-sent by the client (placeholder) using the currently stored config.
        var configResult = await PrepareStoredConfigAsync(entity.ConnectionType, dto.Config, existing: entity, cancellationToken);
        if (configResult.IsFailed)
            return Result.Fail<DatabaseConnectionResponseDto>(configResult.Errors);

        try
        {
            if (await _db.DatabaseConnections.AnyAsync(x => x.Name == dto.Name && x.Id != id, cancellationToken))
                return Result.Fail<DatabaseConnectionResponseDto>(new ConflictError($"A database connection named '{dto.Name}' already exists."));

            entity.Name = dto.Name;
            entity.Description = dto.Description;
            entity.Config = configResult.Value;
            entity.UpdatedBy = performedBy;
            await _db.SaveChangesAsync(cancellationToken);

            await ApplyAutoTestAsync(entity, cancellationToken);

            var auditResult = _auditLog.Log(AuditAction.Update, AuditEntityType.DatabaseConnection, entity.Id, entity.Name, null, performedBy);
            if (auditResult.IsFailed)
                _logger.LogWarning("Failed to log audit entry for database connection update: {Errors}", string.Join(", ", auditResult.Errors));

            return Result.Ok(MapToResponseDto(entity));
        }
        catch (Exception ex)
        {
            return Result.Fail(new InternalError($"Failed to update database connection {id}.", ex));
        }
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(Guid id, string performedBy, CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = await _db.DatabaseConnections.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (entity is null)
                return Result.Fail(new NotFoundError(nameof(DatabaseConnection), id));

            if (entity.IsInternal)
                return Result.Fail(new ForbiddenError("The Tyto Internal connection is system-managed and cannot be deleted."));

            _db.DatabaseConnections.Remove(entity);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Database connection '{Name}' deleted by {User}", entity.Name, performedBy);

            var auditResult = _auditLog.Log(AuditAction.Delete, AuditEntityType.DatabaseConnection, id, entity.Name, null, performedBy);
            if (auditResult.IsFailed)
                _logger.LogWarning("Failed to log audit entry for database connection deletion: {Errors}", string.Join(", ", auditResult.Errors));

            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail(new InternalError($"Failed to delete database connection {id}.", ex));
        }
    }

    /// <inheritdoc />
    public async Task<Result<TestDatabaseConnectionResultDto>> TestConnectionAsync(TestDatabaseConnectionDto dto, CancellationToken cancellationToken = default)
    {
        // Internal connections are system-managed and never tested through this endpoint.
        if (dto.ConnectionType == ConnectionType.InternalSql)
            return Result.Fail<TestDatabaseConnectionResultDto>(
                new ForbiddenError("The Tyto Internal connection is system-managed and cannot be tested."));

        if (dto.ConnectionType == ConnectionType.CosmosDb)
            return Result.Ok(new TestDatabaseConnectionResultDto(false, "Azure Cosmos DB connections are coming soon.", null));

        var validation = await _testValidator.ValidateToResultAsync(dto, cancellationToken);
        if (validation.IsFailed)
            return Result.Fail<TestDatabaseConnectionResultDto>(validation.Errors);

        try
        {
            switch (dto.ConnectionType)
            {
                case ConnectionType.Salesforce:
                    {
                        var cfgResult = await ReadAndValidateSalesforceAsync(dto.Config, cancellationToken);
                        return cfgResult.IsFailed
                            ? Result.Fail<TestDatabaseConnectionResultDto>(cfgResult.Errors)
                            : await TestSalesforceConnectionAsync(cfgResult.Value, cancellationToken);
                    }
                case ConnectionType.Dataverse:
                    {
                        var cfgResult = await ReadAndValidateDataverseAsync(dto.Config, cancellationToken);
                        return cfgResult.IsFailed
                            ? Result.Fail<TestDatabaseConnectionResultDto>(cfgResult.Errors)
                            : await TestDataverseConnectionAsync(cfgResult.Value, cancellationToken);
                    }
                default:
                    return Result.Ok(new TestDatabaseConnectionResultDto(false, $"Unsupported connection type: {dto.ConnectionType}", null));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error testing database connection");
            return Result.Fail<TestDatabaseConnectionResultDto>(
                new InternalError("Failed to test database connection.", ex));
        }
    }

    // ----- Config preparation, encryption and masking ---------------------------------------------

    /// <summary>
    /// Deserializes and validates the incoming Config payload for the given type, encrypts its secret
    /// members (preserving existing values sent as the masked placeholder), and returns the stored
    /// JSON string.
    /// </summary>
    private async Task<Result<string?>> PrepareStoredConfigAsync(
        ConnectionType type, JsonObject? incoming, DatabaseConnection? existing, CancellationToken cancellationToken)
    {
        switch (type)
        {
            case ConnectionType.Salesforce:
                {
                    var cfgResult = await ReadAndValidateSalesforceAsync(incoming, cancellationToken);
                    if (cfgResult.IsFailed)
                        return Result.Fail<string?>(cfgResult.Errors);

                    var cfg = cfgResult.Value;
                    var prev = existing?.GetSalesforceConfig();
                    cfg.ClientSecret = ResolveSecret(cfg.ClientSecret, prev?.ClientSecret, _sfProtector);
                    cfg.PrivateKeyFile = ResolveSecret(cfg.PrivateKeyFile, prev?.PrivateKeyFile, _sfProtector);
                    cfg.Passphrase = ResolveSecret(cfg.Passphrase, prev?.Passphrase, _sfProtector);
                    return Result.Ok<string?>(ConnectionConfigSerializer.Serialize(cfg));
                }
            case ConnectionType.Dataverse:
                {
                    var cfgResult = await ReadAndValidateDataverseAsync(incoming, cancellationToken);
                    if (cfgResult.IsFailed)
                        return Result.Fail<string?>(cfgResult.Errors);

                    var cfg = cfgResult.Value;
                    var prev = existing?.GetDataverseConfig();
                    cfg.ClientSecret = ResolveSecret(cfg.ClientSecret, prev?.ClientSecret, _dvProtector);
                    cfg.CertificateData = ResolveSecret(cfg.CertificateData, prev?.CertificateData, _dvProtector);
                    return Result.Ok<string?>(ConnectionConfigSerializer.Serialize(cfg));
                }
            default:
                // Internal/Cosmos connections are not creatable/updatable through this path.
                return Result.Ok<string?>(null);
        }
    }

    private async Task<Result<SalesforceConfig>> ReadAndValidateSalesforceAsync(JsonObject? incoming, CancellationToken cancellationToken)
    {
        if (!TryDeserialize<SalesforceConfig>(incoming, out var cfg))
            return Result.Fail<SalesforceConfig>(new ValidationError("The Salesforce configuration payload is not valid JSON."));

        var validation = await _salesforceConfigValidator.ValidateToResultAsync(cfg, cancellationToken);
        return validation.IsFailed ? Result.Fail<SalesforceConfig>(validation.Errors) : Result.Ok(cfg);
    }

    private async Task<Result<DataverseConfig>> ReadAndValidateDataverseAsync(JsonObject? incoming, CancellationToken cancellationToken)
    {
        if (!TryDeserialize<DataverseConfig>(incoming, out var cfg))
            return Result.Fail<DataverseConfig>(new ValidationError("The Dataverse configuration payload is not valid JSON."));

        var validation = await _dataverseConfigValidator.ValidateToResultAsync(cfg, cancellationToken);
        return validation.IsFailed ? Result.Fail<DataverseConfig>(validation.Errors) : Result.Ok(cfg);
    }

    private static bool TryDeserialize<T>(JsonObject? incoming, out T value) where T : class, new()
    {
        if (incoming is null)
        {
            value = new T();
            return true;
        }

        try
        {
            value = incoming.Deserialize<T>(ConnectionConfigSerializer.Options) ?? new T();
            return true;
        }
        catch (JsonException)
        {
            value = new T();
            return false;
        }
    }

    /// <summary>
    /// Chooses the value to persist for a secret: the previously stored (encrypted) value when the
    /// client sends nothing or the masked placeholder, otherwise the freshly encrypted new value.
    /// </summary>
    private static string? ResolveSecret(string? incoming, string? existingEncrypted, IDataProtector protector)
    {
        if (string.IsNullOrWhiteSpace(incoming) || incoming == SecretPlaceholder)
            return existingEncrypted;
        return protector.Protect(incoming);
    }

    private static string? TryDecrypt(string? encrypted, IDataProtector protector)
    {
        if (string.IsNullOrWhiteSpace(encrypted)) return null;
        try
        {
            return protector.Unprotect(encrypted);
        }
        catch
        {
            return null;
        }
    }

    private JsonObject? MaskConfig(DatabaseConnection entity)
    {
        switch (entity.ConnectionType)
        {
            case ConnectionType.Salesforce:
                {
                    var cfg = entity.GetSalesforceConfig();
                    if (cfg is null) return null;
                    cfg.ClientSecret = Mask(cfg.ClientSecret);
                    cfg.PrivateKeyFile = Mask(cfg.PrivateKeyFile);
                    cfg.Passphrase = Mask(cfg.Passphrase);
                    return ToJsonObject(cfg);
                }
            case ConnectionType.Dataverse:
                {
                    var cfg = entity.GetDataverseConfig();
                    if (cfg is null) return null;
                    cfg.ClientSecret = Mask(cfg.ClientSecret);
                    cfg.CertificateData = Mask(cfg.CertificateData);
                    return ToJsonObject(cfg);
                }
            default:
                return null;
        }
    }

    private static string? Mask(string? storedValue) => string.IsNullOrEmpty(storedValue) ? null : SecretPlaceholder;

    private static JsonObject? ToJsonObject<T>(T cfg) where T : class =>
        JsonSerializer.SerializeToNode(cfg, ConnectionConfigSerializer.Options)?.AsObject();

    private DatabaseConnectionResponseDto MapToResponseDto(DatabaseConnection entity) =>
        new(
            Id: entity.Id,
            Name: entity.Name,
            Description: entity.Description,
            ConnectionType: entity.ConnectionType,
            IsInternal: entity.IsInternal,
            ConfigurationsCount: entity.Configurations.Count,
            LastTestDate: entity.LastTestDate,
            LastTestStatus: entity.LastTestStatus,
            LastTestStatusCode: entity.LastTestStatusCode,
            LastTestMessage: entity.LastTestMessage,
            Config: MaskConfig(entity),
            CreatedAt: entity.CreatedAt,
            UpdatedAt: entity.UpdatedAt,
            CreatedBy: entity.CreatedBy,
            UpdatedBy: entity.UpdatedBy
        );

    // ----- Connection testing ---------------------------------------------------------------------

    /// <summary>Tests a freshly persisted external connection and records the LastTest* fields.</summary>
    private async Task ApplyAutoTestAsync(DatabaseConnection entity, CancellationToken cancellationToken)
    {
        Result<TestDatabaseConnectionResultDto> testResult;
        switch (entity.ConnectionType)
        {
            case ConnectionType.Salesforce:
                testResult = await TestSalesforceConnectionAsync(DecryptSalesforce(entity), cancellationToken);
                break;
            case ConnectionType.Dataverse:
                testResult = await TestDataverseConnectionAsync(DecryptDataverse(entity), cancellationToken);
                break;
            default:
                return;
        }

        if (testResult.IsFailed)
            return;

        entity.LastTestDate = DateTime.UtcNow;
        entity.LastTestStatus = testResult.Value.IsSuccess ? "Success" : "Failed";
        entity.LastTestStatusCode = testResult.Value.StatusCode;
        entity.LastTestMessage = testResult.Value.Message;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private SalesforceConfig DecryptSalesforce(DatabaseConnection entity)
    {
        var cfg = entity.GetSalesforceConfig() ?? new SalesforceConfig();
        cfg.ClientSecret = TryDecrypt(cfg.ClientSecret, _sfProtector);
        cfg.PrivateKeyFile = TryDecrypt(cfg.PrivateKeyFile, _sfProtector);
        cfg.Passphrase = TryDecrypt(cfg.Passphrase, _sfProtector);
        return cfg;
    }

    private DataverseConfig DecryptDataverse(DatabaseConnection entity)
    {
        var cfg = entity.GetDataverseConfig() ?? new DataverseConfig();
        cfg.ClientSecret = TryDecrypt(cfg.ClientSecret, _dvProtector);
        cfg.CertificateData = TryDecrypt(cfg.CertificateData, _dvProtector);
        return cfg;
    }

    private async Task<Result<TestDatabaseConnectionResultDto>> TestSalesforceConnectionAsync(
        SalesforceConfig config, CancellationToken cancellationToken)
    {
        // TODO: Implement actual Salesforce REST API connection test (SELECT Id FROM User LIMIT 1).
        _logger.LogInformation("Salesforce connection test requested for {InstanceUrl}", config.InstanceUrl);

        await Task.CompletedTask;

        return Result.Ok(new TestDatabaseConnectionResultDto(
            false,
            "Salesforce connection testing not yet implemented. Integration with Salesforce SDK pending.",
            null));
    }

    private async Task<Result<TestDatabaseConnectionResultDto>> TestDataverseConnectionAsync(
        DataverseConfig config, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Dataverse connection test requested for {EnvironmentUrl} with {AuthMethod}",
            config.EnvironmentUrl, config.AuthMethod);

        try
        {
            return config.AuthMethod switch
            {
                DataverseAuthMethod.ClientSecret => await TestDataverseClientSecretAsync(config, cancellationToken),
                DataverseAuthMethod.Certificate => Result.Ok(new TestDatabaseConnectionResultDto(
                    false, "Certificate authentication not yet implemented for Dataverse.", null)),
                DataverseAuthMethod.ManagedIdentity => Result.Ok(new TestDatabaseConnectionResultDto(
                    false, "Managed Identity authentication not yet implemented for Dataverse.", null)),
                _ => Result.Ok(new TestDatabaseConnectionResultDto(
                    false, $"Unsupported Dataverse authentication method: {config.AuthMethod}", null))
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error testing Dataverse connection");
            return Result.Fail(new InternalError("Failed to test Dataverse connection.", ex));
        }
    }

    private async Task<Result<TestDatabaseConnectionResultDto>> TestDataverseClientSecretAsync(
        DataverseConfig config, CancellationToken cancellationToken)
    {
        try
        {
            var app = ConfidentialClientApplicationBuilder
                .Create(config.ClientId)
                .WithClientSecret(config.ClientSecret!)
                .WithAuthority($"https://login.microsoftonline.com/{config.TenantId}")
                .Build();

            var scopes = new[] { $"{config.EnvironmentUrl}/.default" };
            var authResult = await app.AcquireTokenForClient(scopes).ExecuteAsync(cancellationToken);

            _logger.LogInformation("Successfully acquired token for Dataverse environment {EnvironmentUrl}",
                config.EnvironmentUrl);

            var httpClient = _httpClientFactory.CreateClient(ExternalHttpClients.ConnectionTest);
            httpClient.Timeout = TimeSpan.FromSeconds(15);
            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", authResult.AccessToken);

            var whoAmIUrl = $"{config.EnvironmentUrl?.TrimEnd('/')}/api/data/v9.2/WhoAmI";
            var response = await httpClient.GetAsync(whoAmIUrl, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var whoAmI = JsonSerializer.Deserialize<WhoAmIResponse>(content, options);

                _logger.LogInformation("Dataverse connection successful. User ID: {UserId}", whoAmI?.UserId);

                return Result.Ok(new TestDatabaseConnectionResultDto(
                    true, $"Connection successful. User ID: {whoAmI?.UserId}", (int)response.StatusCode));
            }

            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Dataverse WhoAmI request failed with status {StatusCode}: {Error}",
                (int)response.StatusCode, errorContent);

            var errorMessage = (int)response.StatusCode switch
            {
                401 => "Authentication failed. Please verify your credentials.",
                403 => "Authenticated but access denied. Check app permissions in Azure AD.",
                _ => $"Connection failed with HTTP {(int)response.StatusCode}"
            };

            return Result.Ok(new TestDatabaseConnectionResultDto(false, errorMessage, (int)response.StatusCode));
        }
        catch (MsalException ex)
        {
            _logger.LogWarning(ex, "Dataverse authentication failed");
            return Result.Ok(new TestDatabaseConnectionResultDto(false, $"Authentication failed: {ex.Message}", null));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Dataverse connection failed");
            return Result.Ok(new TestDatabaseConnectionResultDto(false, $"Connection failed: {ex.Message}", null));
        }
    }

    private class WhoAmIResponse
    {
        public Guid BusinessUnitId { get; set; }
        public Guid UserId { get; set; }
        public Guid OrganizationId { get; set; }
    }
}
