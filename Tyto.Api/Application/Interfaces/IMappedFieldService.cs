using FluentResults;
using Tyto.Api.Application.Common;
using Tyto.Api.Application.DTOs.MappedField;

namespace Tyto.Api.Application.Interfaces;

/// <summary>
/// Service interface for managing mapped fields within a configuration.
/// </summary>
public interface IMappedFieldService
{
    /// <summary>Gets a paged list of mapped fields for a configuration.</summary>
    Task<Result<PagedResult<MappedFieldResponseDto>>> GetAllAsync(Guid configurationId, QueryParameters parameters, CancellationToken cancellationToken = default);

    /// <summary>Gets a mapped field by its identifier. Returns NotFoundError if not found.</summary>
    Task<Result<MappedFieldResponseDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Creates a new mapped field. Returns ValidationError, ConflictError, or NotFoundError (for invalid FK references) on failure.</summary>
    Task<Result<MappedFieldResponseDto>> CreateAsync(MappedFieldCreateDto dto, string performedBy, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing mapped field. Returns NotFoundError, ValidationError, or ConflictError (e.g., self-reference) on failure.</summary>
    Task<Result<MappedFieldResponseDto>> UpdateAsync(Guid id, MappedFieldUpdateDto dto, string performedBy, CancellationToken cancellationToken = default);

    /// <summary>Deletes a mapped field by its identifier. Returns NotFoundError if not found.</summary>
    Task<Result> DeleteAsync(Guid id, string performedBy, CancellationToken cancellationToken = default);
}
