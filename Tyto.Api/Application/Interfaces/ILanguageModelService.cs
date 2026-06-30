using Tyto.Api.Application.Common;
using Tyto.Api.Application.DTOs.LanguageModel;
using FluentResults;

namespace Tyto.Api.Application.Interfaces;

/// <summary>
/// Service interface for managing language model configurations.
/// </summary>
public interface ILanguageModelService
{
    /// <summary>Gets a paged list of language models.</summary>
    Task<Result<PagedResult<LanguageModelResponseDto>>> GetAllAsync(QueryParameters parameters, CancellationToken cancellationToken = default);

    /// <summary>Gets a language model by its identifier. Returns NotFoundError if not found.</summary>
    Task<Result<LanguageModelResponseDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Creates a new language model. Returns ValidationError or ConflictError on failure.</summary>
    Task<Result<LanguageModelResponseDto>> CreateAsync(LanguageModelCreateDto dto, string performedBy, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing language model. Returns NotFoundError, ValidationError, or ConflictError on failure.</summary>
    Task<Result<LanguageModelResponseDto>> UpdateAsync(Guid id, LanguageModelUpdateDto dto, string performedBy, CancellationToken cancellationToken = default);

    /// <summary>Deletes a language model by its identifier. Returns NotFoundError if not found.</summary>
    Task<Result> DeleteAsync(Guid id, string performedBy, CancellationToken cancellationToken = default);

    /// <summary>Marks the specified language model as the default, unsetting any previous default. Returns NotFoundError if not found.</summary>
    Task<Result<LanguageModelResponseDto>> SetDefaultAsync(Guid id, string performedBy, CancellationToken cancellationToken = default);

    /// <summary>Tests a language model connection without persisting results. Returns connection test result with status and message.</summary>
    Task<Result<TestLanguageModelConnectionResultDto>> TestConnectionAsync(TestLanguageModelConnectionDto dto, CancellationToken cancellationToken = default);
}
