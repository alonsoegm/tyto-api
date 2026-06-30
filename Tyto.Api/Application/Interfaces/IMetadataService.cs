using Tyto.Api.Application.DTOs.Metadata;
using FluentResults;

namespace Tyto.Api.Application.Interfaces;

/// <summary>
/// Resolves a saved <c>DatabaseConnection</c> and delegates to the matching
/// <see cref="IMetadataProvider"/> to retrieve external metadata in a provider-agnostic shape.
/// </summary>
public interface IMetadataService
{
    /// <summary>
    /// Gets the entities (tables) for the given database connection. Returns NotFoundError if the
    /// connection does not exist, or InternalError if no provider supports its connection type.
    /// </summary>
    Task<Result<List<EntityDto>>> GetEntitiesAsync(Guid databaseConnectionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the fields (columns) for the given entity within the given database connection.
    /// Returns NotFoundError if the connection does not exist, or InternalError if no provider
    /// supports its connection type.
    /// </summary>
    Task<Result<List<FieldDto>>> GetFieldsAsync(Guid databaseConnectionId, string entityId, CancellationToken cancellationToken = default);
}
