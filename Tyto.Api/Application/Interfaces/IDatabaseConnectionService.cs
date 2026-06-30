using Tyto.Api.Application.Common;
using Tyto.Api.Application.DTOs.DatabaseConnection;
using FluentResults;

namespace Tyto.Api.Application.Interfaces;

/// <summary>
/// Service interface for managing CRM database connections.
/// </summary>
public interface IDatabaseConnectionService
{
    /// <summary>Gets a paged list of database connections.</summary>
    Task<Result<PagedResult<DatabaseConnectionResponseDto>>> GetAllAsync(QueryParameters parameters, CancellationToken cancellationToken = default);

    /// <summary>Gets a database connection by its identifier. Returns NotFoundError if not found.</summary>
    Task<Result<DatabaseConnectionResponseDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Creates a new database connection. Returns ValidationError or ConflictError on failure.</summary>
    Task<Result<DatabaseConnectionResponseDto>> CreateAsync(DatabaseConnectionCreateDto dto, string performedBy, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing database connection. Returns NotFoundError, ValidationError, or ConflictError on failure.</summary>
    Task<Result<DatabaseConnectionResponseDto>> UpdateAsync(Guid id, DatabaseConnectionUpdateDto dto, string performedBy, CancellationToken cancellationToken = default);

    /// <summary>Deletes a database connection by its identifier. Returns NotFoundError if not found.</summary>
    Task<Result> DeleteAsync(Guid id, string performedBy, CancellationToken cancellationToken = default);

    /// <summary>Tests a database connection ad-hoc from form values without requiring a saved entity.</summary>
    Task<Result<TestDatabaseConnectionResultDto>> TestConnectionAsync(TestDatabaseConnectionDto dto, CancellationToken cancellationToken = default);
}
