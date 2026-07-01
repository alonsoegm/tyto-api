using FluentResults;
using FluentValidation;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Tyto.Api.Application.Common;
using Tyto.Api.Application.Common.Errors;
using Tyto.Api.Application.Common.Validation;
using Tyto.Api.Application.DTOs.MappedField;
using Tyto.Api.Application.Interfaces;
using Tyto.Api.Domain.Entities;
using Tyto.Api.Domain.Enums;
using Tyto.Api.Infrastructure.Data;

namespace Tyto.Api.Application.Services;

public class MappedFieldService : IMappedFieldService
{
    private readonly TytoDbContext _db;
    private readonly IAuditLogService _auditLog;
    private readonly IValidator<MappedFieldCreateDto> _createValidator;
    private readonly IValidator<MappedFieldUpdateDto> _updateValidator;
    private readonly ILogger<MappedFieldService> _logger;

    public MappedFieldService(
        TytoDbContext db,
        IAuditLogService auditLog,
        IValidator<MappedFieldCreateDto> createValidator,
        IValidator<MappedFieldUpdateDto> updateValidator,
        ILogger<MappedFieldService> logger)
    {
        _db = db;
        _auditLog = auditLog;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<PagedResult<MappedFieldResponseDto>>> GetAllAsync(Guid configurationId, QueryParameters parameters, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _db.MappedFields
                .AsNoTracking()
                .Where(x => x.ConfigurationId == configurationId);

            var totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .OrderBy(x => x.SortOrder)
                .Skip((parameters.Page - 1) * parameters.PageSize)
                .Take(parameters.PageSize)
                .ProjectToType<MappedFieldResponseDto>()
                .ToListAsync(cancellationToken);

            var pagedResult = PagedResult<MappedFieldResponseDto>.Create(items, totalCount, parameters.Page, parameters.PageSize);
            return Result.Ok(pagedResult);
        }
        catch (Exception ex)
        {
            return Result.Fail(new InternalError("Failed to retrieve mapped fields.", ex));
        }
    }

    /// <inheritdoc />
    public async Task<Result<MappedFieldResponseDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = await _db.MappedFields.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (entity is null)
                return Result.Fail(new NotFoundError(nameof(MappedField), id));

            var dto = entity.Adapt<MappedFieldResponseDto>();
            return Result.Ok(dto);
        }
        catch (Exception ex)
        {
            return Result.Fail(new InternalError($"Failed to retrieve mapped field {id}.", ex));
        }
    }

    /// <inheritdoc />
    public async Task<Result<MappedFieldResponseDto>> CreateAsync(MappedFieldCreateDto dto, string performedBy, CancellationToken cancellationToken = default)
    {
        var validation = await _createValidator.ValidateToResultAsync(dto, cancellationToken);
        if (validation.IsFailed)
            return Result.Fail<MappedFieldResponseDto>(validation.Errors);

        try
        {
            if (!await _db.Configurations.AnyAsync(x => x.Id == dto.ConfigurationId, cancellationToken))
                return Result.Fail<MappedFieldResponseDto>(new NotFoundError(nameof(Configuration), dto.ConfigurationId));

            if (dto.ParentFieldId.HasValue && !await _db.MappedFields.AnyAsync(x => x.Id == dto.ParentFieldId.Value, cancellationToken))
                return Result.Fail<MappedFieldResponseDto>(new NotFoundError(nameof(MappedField), dto.ParentFieldId.Value));

            var entity = dto.Adapt<MappedField>();
            entity.CreatedBy = performedBy;
            entity.UpdatedBy = performedBy;

            _db.MappedFields.Add(entity);

            _logger.LogInformation("Mapped field '{Name}' created by {User}", entity.FieldName, performedBy);

            var auditResult = _auditLog.Log(AuditAction.Create, AuditEntityType.MappedField, entity.Id, entity.FieldName, null, performedBy);
            if (auditResult.IsFailed)
                _logger.LogWarning("Failed to log audit entry for mapped field creation: {Errors}", string.Join(", ", auditResult.Errors));

            var responseDto = entity.Adapt<MappedFieldResponseDto>();
            return Result.Ok(responseDto);
        }
        catch (Exception ex)
        {
            return Result.Fail(new InternalError("Failed to create mapped field.", ex));
        }
    }

    /// <inheritdoc />
    public async Task<Result<MappedFieldResponseDto>> UpdateAsync(Guid id, MappedFieldUpdateDto dto, string performedBy, CancellationToken cancellationToken = default)
    {
        var validation = await _updateValidator.ValidateToResultAsync(dto, cancellationToken);
        if (validation.IsFailed)
            return Result.Fail<MappedFieldResponseDto>(validation.Errors);

        try
        {
            var entity = await _db.MappedFields.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (entity is null)
                return Result.Fail<MappedFieldResponseDto>(new NotFoundError(nameof(MappedField), id));

            if (dto.ParentFieldId.HasValue && dto.ParentFieldId.Value == id)
                return Result.Fail<MappedFieldResponseDto>(new ConflictError("A field cannot be its own parent."));

            if (dto.ParentFieldId.HasValue && !await _db.MappedFields.AnyAsync(x => x.Id == dto.ParentFieldId.Value, cancellationToken))
                return Result.Fail<MappedFieldResponseDto>(new NotFoundError(nameof(MappedField), dto.ParentFieldId.Value));

            dto.Adapt(entity);
            entity.UpdatedBy = performedBy;

            var auditResult = _auditLog.Log(AuditAction.Update, AuditEntityType.MappedField, entity.Id, entity.FieldName, null, performedBy);
            if (auditResult.IsFailed)
                _logger.LogWarning("Failed to log audit entry for mapped field update: {Errors}", string.Join(", ", auditResult.Errors));

            var responseDto = entity.Adapt<MappedFieldResponseDto>();
            return Result.Ok(responseDto);
        }
        catch (Exception ex)
        {
            return Result.Fail(new InternalError($"Failed to update mapped field {id}.", ex));
        }
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(Guid id, string performedBy, CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = await _db.MappedFields.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (entity is null)
                return Result.Fail(new NotFoundError(nameof(MappedField), id));

            _db.MappedFields.Remove(entity);

            _logger.LogInformation("Mapped field '{Name}' deleted by {User}", entity.FieldName, performedBy);

            var auditResult = _auditLog.Log(AuditAction.Delete, AuditEntityType.MappedField, id, entity.FieldName, null, performedBy);
            if (auditResult.IsFailed)
                _logger.LogWarning("Failed to log audit entry for mapped field deletion: {Errors}", string.Join(", ", auditResult.Errors));

            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail(new InternalError($"Failed to delete mapped field {id}.", ex));
        }
    }
}
