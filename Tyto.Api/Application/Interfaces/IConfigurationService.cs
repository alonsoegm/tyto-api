using FluentResults;
using Tyto.Api.Application.Common;
using Tyto.Api.Application.DTOs.Configuration;

namespace Tyto.Api.Application.Interfaces;

/// <summary>
/// Service interface for managing extraction configurations.
/// </summary>
public interface IConfigurationService
{
    /// <summary>Gets a paged list of configurations.</summary>
    Task<Result<PagedResult<ConfigurationResponseDto>>> GetAllAsync(QueryParameters parameters, CancellationToken cancellationToken = default);

    /// <summary>Gets a configuration by its identifier. Returns NotFoundError if not found.</summary>
    Task<Result<ConfigurationResponseDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Creates a new configuration. Returns ValidationError, ConflictError, or NotFoundError (for invalid FK references) on failure.</summary>
    Task<Result<ConfigurationResponseDto>> CreateAsync(ConfigurationCreateDto dto, string performedBy, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing configuration. Returns NotFoundError, ValidationError, or ConflictError on failure.</summary>
    Task<Result<ConfigurationResponseDto>> UpdateAsync(Guid id, ConfigurationUpdateDto dto, string performedBy, CancellationToken cancellationToken = default);

    /// <summary>Deletes a configuration by its identifier. Returns NotFoundError if not found.</summary>
    Task<Result> DeleteAsync(Guid id, string performedBy, CancellationToken cancellationToken = default);
}
