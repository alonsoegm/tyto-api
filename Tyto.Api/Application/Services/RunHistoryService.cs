using FluentResults;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Tyto.Api.Application.Common;
using Tyto.Api.Application.Common.Errors;
using Tyto.Api.Application.DTOs.RunHistory;
using Tyto.Api.Application.Interfaces;
using Tyto.Api.Domain.Entities;
using Tyto.Api.Infrastructure.Data;

namespace Tyto.Api.Application.Services;

public class RunHistoryService : IRunHistoryService
{
    private readonly TytoDbContext _db;

    public RunHistoryService(TytoDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<Result<PagedResult<RunHistoryResponseDto>>> GetAllAsync(QueryParameters parameters, Guid? configurationId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _db.RunHistories
                .AsNoTracking()
                .Include(x => x.Configuration);

            var filtered = configurationId.HasValue
                ? query.Where(x => x.ConfigurationId == configurationId.Value)
                : query;

            var totalCount = await filtered.CountAsync(cancellationToken);

            var items = await filtered
                .OrderByDescending(x => x.StartedAt)
                .Skip((parameters.Page - 1) * parameters.PageSize)
                .Take(parameters.PageSize)
                .ToListAsync(cancellationToken);

            var dtos = items.Select(x => new RunHistoryResponseDto(
                x.Id, x.ConfigurationId, x.Configuration?.Name ?? string.Empty,
                x.StartedAt, x.CompletedAt, x.Success, x.ErrorMessage,
                x.DocumentsProcessed, x.RecordsCreated, x.RecordsUpdated, x.RecordsFailed,
                x.TriggeredBy, x.CreatedAt
            )).ToList();

            var pagedResult = PagedResult<RunHistoryResponseDto>.Create(dtos, totalCount, parameters.Page, parameters.PageSize);
            return Result.Ok(pagedResult);
        }
        catch (Exception ex)
        {
            return Result.Fail(new InternalError("Failed to retrieve run history.", ex));
        }
    }

    /// <inheritdoc />
    public async Task<Result<RunHistoryResponseDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = await _db.RunHistories
                .AsNoTracking()
                .Include(x => x.Configuration)
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (entity is null)
                return Result.Fail(new NotFoundError(nameof(RunHistory), id));

            var dto = new RunHistoryResponseDto(
                entity.Id, entity.ConfigurationId, entity.Configuration?.Name ?? string.Empty,
                entity.StartedAt, entity.CompletedAt, entity.Success, entity.ErrorMessage,
                entity.DocumentsProcessed, entity.RecordsCreated, entity.RecordsUpdated, entity.RecordsFailed,
                entity.TriggeredBy, entity.CreatedAt
            );
            return Result.Ok(dto);
        }
        catch (Exception ex)
        {
            return Result.Fail(new InternalError($"Failed to retrieve run history {id}.", ex));
        }
    }
}
