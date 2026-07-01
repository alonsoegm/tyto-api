using FluentResults;
using Tyto.Api.Application.Common;
using Tyto.Api.Application.DTOs.RunHistory;

namespace Tyto.Api.Application.Interfaces;

/// <summary>
/// Service interface for querying execution run history.
/// </summary>
public interface IRunHistoryService
{
    /// <summary>Gets a paged list of run history entries, optionally filtered by configuration.</summary>
    Task<Result<PagedResult<RunHistoryResponseDto>>> GetAllAsync(QueryParameters parameters, Guid? configurationId = null, CancellationToken cancellationToken = default);

    /// <summary>Gets a run history entry by its identifier. Returns NotFoundError if not found.</summary>
    Task<Result<RunHistoryResponseDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
