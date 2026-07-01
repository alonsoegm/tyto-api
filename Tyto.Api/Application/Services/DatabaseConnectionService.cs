using System.Net.Http.Headers;
using System.Text.Json;
using FluentResults;
using FluentValidation;
using Mapster;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Client;
using Tyto.Api.Application.Common;
using Tyto.Api.Application.Common.Constants;
using Tyto.Api.Application.Common.Errors;
using Tyto.Api.Application.Common.Validation;
using Tyto.Api.Application.DTOs.DatabaseConnection;
using Tyto.Api.Application.Interfaces;
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
    private readonly ILogger<DatabaseConnectionService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public DatabaseConnectionService(
        TytoDbContext db,
        IAuditLogService auditLog,
        IDataProtectionProvider dataProtection,
        IValidator<DatabaseConnectionCreateDto> createValidator,
        IValidator<DatabaseConnectionUpdateDto> updateValidator,
        IValidator<TestDatabaseConnectionDto> testValidator,
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

        try
        {
            if (await _db.DatabaseConnections.AnyAsync(x => x.Name == dto.Name, cancellationToken))
                return Result.Fail<DatabaseConnectionResponseDto>(new ConflictError($"A database connection named '{dto.Name}' already exists."));

            var entity = dto.Adapt<DatabaseConnection>();
            entity.CreatedBy = performedBy;
            entity.UpdatedBy = performedBy;

            EncryptSecrets(dto, entity);

            _db.DatabaseConnections.Add(entity);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Database connection '{Name}' created by {User}", entity.Name, performedBy);

            // Test connection automatically after creation
            var testDto = CreateTestDto(dto, entity);
            var testResult = await TestConnectionAsync(testDto, cancellationToken);
            if (testResult.IsSuccess)
            {
                entity.LastTestDate = DateTime.UtcNow;
                entity.LastTestStatus = testResult.Value.IsSuccess ? "Success" : "Failed";
                entity.LastTestStatusCode = testResult.Value.StatusCode;
                entity.LastTestMessage = testResult.Value.Message;
                await _db.SaveChangesAsync(cancellationToken);
            }

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
        var validation = await _updateValidator.ValidateToResultAsync(dto, cancellationToken);
        if (validation.IsFailed)
            return Result.Fail<DatabaseConnectionResponseDto>(validation.Errors);

        try
        {
            var entity = await _db.DatabaseConnections
                .Include(x => x.Configurations)
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (entity is null)
                return Result.Fail<DatabaseConnectionResponseDto>(new NotFoundError(nameof(DatabaseConnection), id));

            if (await _db.DatabaseConnections.AnyAsync(x => x.Name == dto.Name && x.Id != id, cancellationToken))
                return Result.Fail<DatabaseConnectionResponseDto>(new ConflictError($"A database connection named '{dto.Name}' already exists."));

            dto.Adapt(entity);
            entity.UpdatedBy = performedBy;

            EncryptSecrets(dto, entity);
            await _db.SaveChangesAsync(cancellationToken);

            // Test connection automatically after update
            var testDto = CreateTestDto(dto, entity);
            var testResult = await TestConnectionAsync(testDto, cancellationToken);
            if (testResult.IsSuccess)
            {
                entity.LastTestDate = DateTime.UtcNow;
                entity.LastTestStatus = testResult.Value.IsSuccess ? "Success" : "Failed";
                entity.LastTestStatusCode = testResult.Value.StatusCode;
                entity.LastTestMessage = testResult.Value.Message;
                await _db.SaveChangesAsync(cancellationToken);
            }

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


    /// <inheritdoc />
    /// <inheritdoc />
    public async Task<Result<TestDatabaseConnectionResultDto>> TestConnectionAsync(TestDatabaseConnectionDto dto, CancellationToken cancellationToken = default)
    {
        var validation = await _testValidator.ValidateToResultAsync(dto, cancellationToken);
        if (validation.IsFailed)
            return Result.Fail<TestDatabaseConnectionResultDto>(validation.Errors);

        try
        {
            return dto.ConnectionType switch
            {
                ConnectionType.Salesforce => await TestSalesforceConnectionAsync(dto, cancellationToken),
                ConnectionType.MsDataverse => await TestDataverseConnectionAsync(dto, cancellationToken),
                _ => Result.Ok(new TestDatabaseConnectionResultDto(
                    false,
                    $"Unsupported connection type: {dto.ConnectionType}",
                    null))
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error testing database connection");
            return Result.Fail<TestDatabaseConnectionResultDto>(
                new InternalError("Failed to test database connection.", ex));
        }
    }

    private async Task<Result<TestDatabaseConnectionResultDto>> TestSalesforceConnectionAsync(
        TestDatabaseConnectionDto dto,
        CancellationToken cancellationToken)
    {
        // TODO: Implement actual Salesforce REST API connection test
        // Should use Salesforce REST API to make a simple query (e.g., SELECT Id FROM User LIMIT 1)
        // Handle OAuth2 authentication based on SF_AuthMethod
        _logger.LogInformation("Salesforce connection test requested for {InstanceUrl}", dto.SF_InstanceUrl);

        await Task.CompletedTask; // Remove when implementing

        return Result.Ok(new TestDatabaseConnectionResultDto(
            false,
            "Salesforce connection testing not yet implemented. Integration with Salesforce SDK pending.",
            null));
    }

    private async Task<Result<TestDatabaseConnectionResultDto>> TestDataverseConnectionAsync(
        TestDatabaseConnectionDto dto,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Dataverse connection test requested for {EnvironmentUrl} with {AuthMethod}",
            dto.DV_EnvironmentUrl, dto.DV_AuthMethod);

        try
        {
            return dto.DV_AuthMethod switch
            {
                DataverseAuthMethod.ClientSecret => await TestDataverseClientSecretAsync(dto, cancellationToken),
                DataverseAuthMethod.Certificate => Result.Ok(new TestDatabaseConnectionResultDto(
                    false,
                    "Certificate authentication not yet implemented for Dataverse.",
                    null)),
                DataverseAuthMethod.ManagedIdentity => Result.Ok(new TestDatabaseConnectionResultDto(
                    false,
                    "Managed Identity authentication not yet implemented for Dataverse.",
                    null)),
                _ => Result.Ok(new TestDatabaseConnectionResultDto(
                    false,
                    $"Unsupported Dataverse authentication method: {dto.DV_AuthMethod}",
                    null))
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error testing Dataverse connection");
            return Result.Fail(new InternalError("Failed to test Dataverse connection.", ex));
        }
    }

    private async Task<Result<TestDatabaseConnectionResultDto>> TestDataverseClientSecretAsync(
        TestDatabaseConnectionDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            // Build MSAL confidential client application
            var app = ConfidentialClientApplicationBuilder
                .Create(dto.DV_ClientId)
                .WithClientSecret(dto.DV_ClientSecret!)
                .WithAuthority($"https://login.microsoftonline.com/{dto.DV_TenantId}")
                .Build();

            // Acquire token for Dataverse scope
            var scopes = new[] { $"{dto.DV_EnvironmentUrl}/.default" };
            var authResult = await app.AcquireTokenForClient(scopes)
                .ExecuteAsync(cancellationToken);

            _logger.LogInformation("Successfully acquired token for Dataverse environment {EnvironmentUrl}",
                dto.DV_EnvironmentUrl);

            // Call WhoAmI endpoint to validate connection
            var httpClient = _httpClientFactory.CreateClient(ExternalHttpClients.ConnectionTest);
            httpClient.Timeout = TimeSpan.FromSeconds(15);
            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", authResult.AccessToken);

            var whoAmIUrl = $"{dto.DV_EnvironmentUrl?.TrimEnd('/')}/api/data/v9.2/WhoAmI";
            var response = await httpClient.GetAsync(whoAmIUrl, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var whoAmI = JsonSerializer.Deserialize<WhoAmIResponse>(content, options);

                _logger.LogInformation("Dataverse connection successful. User ID: {UserId}", whoAmI?.UserId);

                return Result.Ok(new TestDatabaseConnectionResultDto(
                    true,
                    $"Connection successful. User ID: {whoAmI?.UserId}",
                    (int)response.StatusCode));
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

            return Result.Ok(new TestDatabaseConnectionResultDto(
                false,
                errorMessage,
                (int)response.StatusCode));
        }
        catch (MsalException ex)
        {
            _logger.LogWarning(ex, "Dataverse authentication failed");
            return Result.Ok(new TestDatabaseConnectionResultDto(
                false,
                $"Authentication failed: {ex.Message}",
                null));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Dataverse connection failed");
            return Result.Ok(new TestDatabaseConnectionResultDto(
                false,
                $"Connection failed: {ex.Message}",
                null));
        }
    }

    private static DatabaseConnectionResponseDto MapToResponseDto(DatabaseConnection entity) =>
        new(
            Id: entity.Id,
            DisplayName: entity.Name,
            ConnectionType: entity.ConnectionType,
            ConfigurationsCount: entity.Configurations.Count,
            LastTestDate: entity.LastTestDate,
            LastTestStatus: entity.LastTestStatus,
            LastTestStatusCode: entity.LastTestStatusCode,
            LastTestMessage: entity.LastTestMessage,

            // Salesforce
            SF_AuthMethod: entity.SF_AuthMethod,
            SF_InstanceUrl: entity.SF_InstanceUrl,
            SF_Username: entity.SF_Username,
            SF_ConsumerKey: entity.SF_ConsumerKey,
            SF_ApiVersion: entity.SF_ApiVersion,
            SF_RunAsIntegrationUser: entity.SF_RunAsIntegrationUser,
            SF_SigningKeySource: entity.SF_SigningKeySource,
            SF_JwtAudience: entity.SF_JwtAudience,
            SF_KeyVaultUrl: entity.SF_KeyVaultUrl,
            SF_KeyVaultSecretName: entity.SF_KeyVaultSecretName,
            SF_IsSandbox: entity.SF_IsSandbox,
            SF_HasClientSecret: !string.IsNullOrEmpty(entity.SF_ClientSecret),
            SF_HasPrivateKey: !string.IsNullOrEmpty(entity.SF_PrivateKeyFile),

            // Dataverse
            DV_AuthMethod: entity.DV_AuthMethod,
            DV_EnvironmentUrl: entity.DV_EnvironmentUrl,
            DV_TenantId: entity.DV_TenantId,
            DV_ClientId: entity.DV_ClientId,
            DV_CertificateSource: entity.DV_CertificateSource,
            DV_CertificateThumbprint: entity.DV_CertificateThumbprint,
            DV_KeyVaultUrl: entity.DV_KeyVaultUrl,
            DV_KeyVaultCertificateName: entity.DV_KeyVaultCertificateName,
            DV_ManagedIdentityType: entity.DV_ManagedIdentityType,
            DV_UserAssignedClientId: entity.DV_UserAssignedClientId,
            DV_HasClientSecret: !string.IsNullOrEmpty(entity.DV_ClientSecret),

            CreatedAt: entity.CreatedAt,
            UpdatedAt: entity.UpdatedAt,
            CreatedBy: entity.CreatedBy,
            UpdatedBy: entity.UpdatedBy
        );

    private void EncryptSecrets(DatabaseConnectionCreateDto dto, DatabaseConnection entity)
    {
        if (!string.IsNullOrWhiteSpace(dto.SF_ClientSecret))
            entity.SF_ClientSecret = _sfProtector.Protect(dto.SF_ClientSecret);
        if (!string.IsNullOrWhiteSpace(dto.SF_PrivateKeyFile))
            entity.SF_PrivateKeyFile = _sfProtector.Protect(dto.SF_PrivateKeyFile);
        if (!string.IsNullOrWhiteSpace(dto.SF_Passphrase))
            entity.SF_Passphrase = _sfProtector.Protect(dto.SF_Passphrase);
        if (!string.IsNullOrWhiteSpace(dto.DV_ClientSecret))
            entity.DV_ClientSecret = _dvProtector.Protect(dto.DV_ClientSecret);
        if (!string.IsNullOrWhiteSpace(dto.DV_CertificateData))
            entity.DV_CertificateData = _dvProtector.Protect(dto.DV_CertificateData);
    }

    private void EncryptSecrets(DatabaseConnectionUpdateDto dto, DatabaseConnection entity)
    {
        if (!string.IsNullOrWhiteSpace(dto.SF_ClientSecret) && dto.SF_ClientSecret != SecretPlaceholder)
            entity.SF_ClientSecret = _sfProtector.Protect(dto.SF_ClientSecret);
        if (!string.IsNullOrWhiteSpace(dto.SF_PrivateKeyFile) && dto.SF_PrivateKeyFile != SecretPlaceholder)
            entity.SF_PrivateKeyFile = _sfProtector.Protect(dto.SF_PrivateKeyFile);
        if (!string.IsNullOrWhiteSpace(dto.SF_Passphrase) && dto.SF_Passphrase != SecretPlaceholder)
            entity.SF_Passphrase = _sfProtector.Protect(dto.SF_Passphrase);
        if (!string.IsNullOrWhiteSpace(dto.DV_ClientSecret) && dto.DV_ClientSecret != SecretPlaceholder)
            entity.DV_ClientSecret = _dvProtector.Protect(dto.DV_ClientSecret);
        if (!string.IsNullOrWhiteSpace(dto.DV_CertificateData) && dto.DV_CertificateData != SecretPlaceholder)
            entity.DV_CertificateData = _dvProtector.Protect(dto.DV_CertificateData);
    }

    private static TestDatabaseConnectionDto CreateTestDto(DatabaseConnectionCreateDto dto, DatabaseConnection entity) =>
        new(
            ConnectionType: entity.ConnectionType,
            SF_AuthMethod: dto.SF_AuthMethod,
            SF_InstanceUrl: dto.SF_InstanceUrl,
            SF_Username: dto.SF_Username,
            SF_ConsumerKey: dto.SF_ConsumerKey,
            SF_ClientSecret: dto.SF_ClientSecret,
            SF_ApiVersion: dto.SF_ApiVersion,
            SF_RunAsIntegrationUser: dto.SF_RunAsIntegrationUser,
            SF_SigningKeySource: dto.SF_SigningKeySource,
            SF_JwtAudience: dto.SF_JwtAudience,
            SF_PrivateKeyFile: dto.SF_PrivateKeyFile,
            SF_Passphrase: dto.SF_Passphrase,
            SF_KeyVaultUrl: dto.SF_KeyVaultUrl,
            SF_KeyVaultSecretName: dto.SF_KeyVaultSecretName,
            DV_AuthMethod: dto.DV_AuthMethod,
            DV_EnvironmentUrl: dto.DV_EnvironmentUrl,
            DV_TenantId: dto.DV_TenantId,
            DV_ClientId: dto.DV_ClientId,
            DV_ClientSecret: dto.DV_ClientSecret,
            DV_CertificateSource: dto.DV_CertificateSource,
            DV_CertificateData: dto.DV_CertificateData,
            DV_KeyVaultUrl: dto.DV_KeyVaultUrl,
            DV_KeyVaultCertificateName: dto.DV_KeyVaultCertificateName,
            DV_ManagedIdentityType: dto.DV_ManagedIdentityType,
            DV_UserAssignedClientId: dto.DV_UserAssignedClientId
        );

    private TestDatabaseConnectionDto CreateTestDto(DatabaseConnectionUpdateDto dto, DatabaseConnection entity)
    {
        // For update, use provided secrets if not placeholder, otherwise decrypt existing
        string? sfClientSecret = dto.SF_ClientSecret != SecretPlaceholder && !string.IsNullOrWhiteSpace(dto.SF_ClientSecret)
            ? dto.SF_ClientSecret
            : TryDecrypt(entity.SF_ClientSecret, _sfProtector);

        string? sfPrivateKey = dto.SF_PrivateKeyFile != SecretPlaceholder && !string.IsNullOrWhiteSpace(dto.SF_PrivateKeyFile)
            ? dto.SF_PrivateKeyFile
            : TryDecrypt(entity.SF_PrivateKeyFile, _sfProtector);

        string? sfPassphrase = dto.SF_Passphrase != SecretPlaceholder && !string.IsNullOrWhiteSpace(dto.SF_Passphrase)
            ? dto.SF_Passphrase
            : TryDecrypt(entity.SF_Passphrase, _sfProtector);

        string? dvClientSecret = dto.DV_ClientSecret != SecretPlaceholder && !string.IsNullOrWhiteSpace(dto.DV_ClientSecret)
            ? dto.DV_ClientSecret
            : TryDecrypt(entity.DV_ClientSecret, _dvProtector);

        string? dvCertData = dto.DV_CertificateData != SecretPlaceholder && !string.IsNullOrWhiteSpace(dto.DV_CertificateData)
            ? dto.DV_CertificateData
            : TryDecrypt(entity.DV_CertificateData, _dvProtector);

        return new TestDatabaseConnectionDto(
            ConnectionType: entity.ConnectionType,
            SF_AuthMethod: dto.SF_AuthMethod,
            SF_InstanceUrl: dto.SF_InstanceUrl,
            SF_Username: dto.SF_Username,
            SF_ConsumerKey: dto.SF_ConsumerKey,
            SF_ClientSecret: sfClientSecret,
            SF_ApiVersion: dto.SF_ApiVersion,
            SF_RunAsIntegrationUser: dto.SF_RunAsIntegrationUser,
            SF_SigningKeySource: dto.SF_SigningKeySource,
            SF_JwtAudience: dto.SF_JwtAudience,
            SF_PrivateKeyFile: sfPrivateKey,
            SF_Passphrase: sfPassphrase,
            SF_KeyVaultUrl: dto.SF_KeyVaultUrl,
            SF_KeyVaultSecretName: dto.SF_KeyVaultSecretName,
            DV_AuthMethod: dto.DV_AuthMethod,
            DV_EnvironmentUrl: dto.DV_EnvironmentUrl,
            DV_TenantId: dto.DV_TenantId,
            DV_ClientId: dto.DV_ClientId,
            DV_ClientSecret: dvClientSecret,
            DV_CertificateSource: dto.DV_CertificateSource,
            DV_CertificateData: dvCertData,
            DV_KeyVaultUrl: dto.DV_KeyVaultUrl,
            DV_KeyVaultCertificateName: dto.DV_KeyVaultCertificateName,
            DV_ManagedIdentityType: dto.DV_ManagedIdentityType,
            DV_UserAssignedClientId: dto.DV_UserAssignedClientId
        );
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
            return null; // If decryption fails, return null
        }
    }

    private class WhoAmIResponse
    {
        public Guid BusinessUnitId { get; set; }
        public Guid UserId { get; set; }
        public Guid OrganizationId { get; set; }
    }
}
