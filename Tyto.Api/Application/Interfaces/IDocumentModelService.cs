using Tyto.Api.Application.Common;
using Tyto.Api.Application.DTOs.DocumentModel;
using FluentResults;

namespace Tyto.Api.Application.Interfaces;

/// <summary>
/// Service interface for managing document intelligence model configurations.
/// </summary>
public interface IDocumentModelService
{
    /// <summary>Gets a paged list of document models.</summary>
    Task<Result<PagedResult<DocumentModelResponseDto>>> GetAllAsync(QueryParameters parameters, CancellationToken cancellationToken = default);

    /// <summary>Gets a document model by its identifier. Returns NotFoundError if not found.</summary>
    Task<Result<DocumentModelResponseDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Creates a new document model. Returns ValidationError or ConflictError on failure.</summary>
    Task<Result<DocumentModelResponseDto>> CreateAsync(DocumentModelCreateDto dto, string performedBy, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing document model. Returns NotFoundError, ValidationError, or ConflictError on failure.</summary>
    Task<Result<DocumentModelResponseDto>> UpdateAsync(Guid id, DocumentModelUpdateDto dto, string performedBy, CancellationToken cancellationToken = default);

    /// <summary>Deletes a document model by its identifier. Returns NotFoundError if not found.</summary>
    Task<Result> DeleteAsync(Guid id, string performedBy, CancellationToken cancellationToken = default);

    /// <summary>Marks the specified document model as the default, unsetting any previous default. Returns NotFoundError if not found.</summary>
    Task<Result<DocumentModelResponseDto>> SetDefaultAsync(Guid id, string performedBy, CancellationToken cancellationToken = default);

    /// <summary>Tests a document intelligence connection using request values only. Returns ValidationError on validation failure.</summary>
    Task<Result<TestDocumentModelConnectionResultDto>> TestConnectionAsync(
        TestDocumentModelConnectionDto dto,
        CancellationToken cancellationToken = default);
}
