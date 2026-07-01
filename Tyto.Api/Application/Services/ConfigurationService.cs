using System.Text.Json;
using FluentResults;
using FluentValidation;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Tyto.Api.Application.Common;
using Tyto.Api.Application.Common.Errors;
using Tyto.Api.Application.Common.Validation;
using Tyto.Api.Application.DTOs.Configuration;
using Tyto.Api.Application.Interfaces;
using Tyto.Api.Domain.Entities;
using Tyto.Api.Domain.Enums;
using Tyto.Api.Infrastructure.Data;

namespace Tyto.Api.Application.Services;

public class ConfigurationService : IConfigurationService
{
    private readonly TytoDbContext _db;
    private readonly IAuditLogService _auditLog;
    private readonly IValidator<ConfigurationCreateDto> _createValidator;
    private readonly IValidator<ConfigurationUpdateDto> _updateValidator;
    private readonly ILogger<ConfigurationService> _logger;

    public ConfigurationService(
        TytoDbContext db,
        IAuditLogService auditLog,
        IValidator<ConfigurationCreateDto> createValidator,
        IValidator<ConfigurationUpdateDto> updateValidator,
        ILogger<ConfigurationService> logger)
    {
        _db = db;
        _auditLog = auditLog;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<PagedResult<ConfigurationResponseDto>>> GetAllAsync(QueryParameters parameters, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _db.Configurations
                .AsNoTracking()
                .Include(x => x.LanguageModel)
                .Include(x => x.DocumentModel)
                .Include(x => x.DatabaseConnection)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(parameters.Search))
                query = query.Where(x => x.Name.Contains(parameters.Search) || x.Description.Contains(parameters.Search));

            var totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .OrderBy(x => x.Name)
                .Skip((parameters.Page - 1) * parameters.PageSize)
                .Take(parameters.PageSize)
                .ToListAsync(cancellationToken);

            var dtos = items.Select(x => MapToResponseDto(x)).ToList();
            var pagedResult = PagedResult<ConfigurationResponseDto>.Create(dtos, totalCount, parameters.Page, parameters.PageSize);
            return Result.Ok(pagedResult);
        }
        catch (Exception ex)
        {
            return Result.Fail(new InternalError("Failed to retrieve configurations.", ex));
        }
    }

    /// <inheritdoc />
    public async Task<Result<ConfigurationResponseDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = await _db.Configurations
                .AsNoTracking()
                .Include(x => x.LanguageModel)
                .Include(x => x.DocumentModel)
                .Include(x => x.DatabaseConnection)
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (entity is null)
                return Result.Fail(new NotFoundError(nameof(Configuration), id));

            var dto = MapToResponseDto(entity);
            return Result.Ok(dto);
        }
        catch (Exception ex)
        {
            return Result.Fail(new InternalError($"Failed to retrieve configuration {id}.", ex));
        }
    }

    /// <inheritdoc />
    public async Task<Result<ConfigurationResponseDto>> CreateAsync(ConfigurationCreateDto dto, string performedBy, CancellationToken cancellationToken = default)
    {
        var validation = await _createValidator.ValidateToResultAsync(dto, cancellationToken);
        if (validation.IsFailed)
            return Result.Fail<ConfigurationResponseDto>(validation.Errors);

        try
        {
            if (await _db.Configurations.AnyAsync(x => x.Name == dto.Name, cancellationToken))
                return Result.Fail<ConfigurationResponseDto>(new ConflictError($"A configuration named '{dto.Name}' already exists."));

            if (!await _db.LanguageModels.AnyAsync(x => x.Id == dto.LanguageModelId, cancellationToken))
                return Result.Fail<ConfigurationResponseDto>(new NotFoundError(nameof(LanguageModel), dto.LanguageModelId));

            if (!await _db.DatabaseConnections.AnyAsync(x => x.Id == dto.DatabaseConnectionId, cancellationToken))
                return Result.Fail<ConfigurationResponseDto>(new NotFoundError(nameof(DatabaseConnection), dto.DatabaseConnectionId));

            if (dto.DocumentModelId.HasValue && !await _db.DocumentModels.AnyAsync(x => x.Id == dto.DocumentModelId.Value, cancellationToken))
                return Result.Fail<ConfigurationResponseDto>(new NotFoundError(nameof(DocumentModel), dto.DocumentModelId.Value));

            var entity = dto.Adapt<Configuration>();
            entity.CreatedBy = performedBy;
            entity.UpdatedBy = performedBy;
            entity.MaxUploadSizeMB = dto.MaxUploadSizeMB;
            entity.AcceptedFileTypes = JsonSerializer.Serialize(dto.AcceptedFileTypes);

            for (int i = 0; i < dto.MappedFields.Count; i++)
            {
                var f = dto.MappedFields[i];
                entity.MappedFields.Add(new MappedField
                {
                    FieldName = f.FieldName,
                    DisplayLabel = f.DisplayLabel,
                    FieldType = f.FieldType,
                    RequirementLevel = f.RequirementLevel,
                    ExtractionHint = f.ExtractionHint,
                    DefaultValue = f.DefaultValue,
                    SortOrder = i,
                    CreatedBy = performedBy,
                    UpdatedBy = performedBy,
                });
            }

            _db.Configurations.Add(entity);

            // Load related entity names for the response
            var languageModelName = await _db.LanguageModels
                .Where(x => x.Id == dto.LanguageModelId)
                .Select(x => x.Name)
                .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

            var documentModelName = dto.DocumentModelId.HasValue
                ? await _db.DocumentModels
                    .Where(x => x.Id == dto.DocumentModelId.Value)
                    .Select(x => x.Name)
                    .FirstOrDefaultAsync(cancellationToken)
                : null;

            var databaseConnectionName = await _db.DatabaseConnections
                .Where(x => x.Id == dto.DatabaseConnectionId)
                .Select(x => x.Name)
                .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

            _logger.LogInformation("Configuration '{Name}' created by {User} with {FieldCount} mapped fields", entity.Name, performedBy, entity.MappedFields.Count);

            var auditResult = _auditLog.Log(AuditAction.Create, AuditEntityType.Configuration, entity.Id, entity.Name, null, performedBy);
            if (auditResult.IsFailed)
                _logger.LogWarning("Failed to log audit entry for configuration creation: {Errors}", string.Join(", ", auditResult.Errors));

            return Result.Ok(new ConfigurationResponseDto(
                entity.Id, entity.Name, entity.Description, entity.Status,
                entity.ExtractionStrategy, entity.ModelSelectionMode,
                entity.LanguageModelId, languageModelName,
                entity.DocumentModelId, documentModelName,
                entity.DatabaseConnectionId, databaseConnectionName,
                entity.TargetObject, entity.SystemPrompt, entity.UserPromptTemplate,
                entity.MaxTokens, entity.Temperature,
                entity.CreatedAt, entity.UpdatedAt, entity.CreatedBy, entity.UpdatedBy
            ));
        }
        catch (Exception ex)
        {
            return Result.Fail(new InternalError("Failed to create configuration.", ex));
        }
    }

    /// <inheritdoc />
    public async Task<Result<ConfigurationResponseDto>> UpdateAsync(Guid id, ConfigurationUpdateDto dto, string performedBy, CancellationToken cancellationToken = default)
    {
        var validation = await _updateValidator.ValidateToResultAsync(dto, cancellationToken);
        if (validation.IsFailed)
            return Result.Fail<ConfigurationResponseDto>(validation.Errors);

        try
        {
            var entity = await _db.Configurations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (entity is null)
                return Result.Fail<ConfigurationResponseDto>(new NotFoundError(nameof(Configuration), id));

            if (await _db.Configurations.AnyAsync(x => x.Name == dto.Name && x.Id != id, cancellationToken))
                return Result.Fail<ConfigurationResponseDto>(new ConflictError($"A configuration named '{dto.Name}' already exists."));

            if (!await _db.LanguageModels.AnyAsync(x => x.Id == dto.LanguageModelId, cancellationToken))
                return Result.Fail<ConfigurationResponseDto>(new NotFoundError(nameof(LanguageModel), dto.LanguageModelId));

            if (!await _db.DatabaseConnections.AnyAsync(x => x.Id == dto.DatabaseConnectionId, cancellationToken))
                return Result.Fail<ConfigurationResponseDto>(new NotFoundError(nameof(DatabaseConnection), dto.DatabaseConnectionId));

            if (dto.DocumentModelId.HasValue && !await _db.DocumentModels.AnyAsync(x => x.Id == dto.DocumentModelId.Value, cancellationToken))
                return Result.Fail<ConfigurationResponseDto>(new NotFoundError(nameof(DocumentModel), dto.DocumentModelId.Value));

            dto.Adapt(entity);
            entity.UpdatedBy = performedBy;

            var auditResult = _auditLog.Log(AuditAction.Update, AuditEntityType.Configuration, entity.Id, entity.Name, null, performedBy);
            if (auditResult.IsFailed)
                _logger.LogWarning("Failed to log audit entry for configuration update: {Errors}", string.Join(", ", auditResult.Errors));

            var getByIdResult = await GetByIdAsync(id, cancellationToken);
            return getByIdResult;
        }
        catch (Exception ex)
        {
            return Result.Fail(new InternalError($"Failed to update configuration {id}.", ex));
        }
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(Guid id, string performedBy, CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = await _db.Configurations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (entity is null)
                return Result.Fail(new NotFoundError(nameof(Configuration), id));

            _db.Configurations.Remove(entity);

            _logger.LogInformation("Configuration '{Name}' deleted by {User}", entity.Name, performedBy);

            var auditResult = _auditLog.Log(AuditAction.Delete, AuditEntityType.Configuration, id, entity.Name, null, performedBy);
            if (auditResult.IsFailed)
                _logger.LogWarning("Failed to log audit entry for configuration deletion: {Errors}", string.Join(", ", auditResult.Errors));

            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail(new InternalError($"Failed to delete configuration {id}.", ex));
        }
    }

    private static ConfigurationResponseDto MapToResponseDto(Configuration x) =>
        new(
            x.Id, x.Name, x.Description, x.Status,
            x.ExtractionStrategy, x.ModelSelectionMode,
            x.LanguageModelId, x.LanguageModel?.Name ?? string.Empty,
            x.DocumentModelId, x.DocumentModel?.Name,
            x.DatabaseConnectionId, x.DatabaseConnection?.Name ?? string.Empty,
            x.TargetObject, x.SystemPrompt, x.UserPromptTemplate,
            x.MaxTokens, x.Temperature,
            x.CreatedAt, x.UpdatedAt, x.CreatedBy, x.UpdatedBy
        );
}
