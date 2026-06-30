using Tyto.Api.Application.DTOs.Metadata;
using Tyto.Api.Domain.Entities;
using Tyto.Api.Domain.Enums;
using FluentResults;

namespace Tyto.Api.Application.Interfaces;

/// <summary>
/// Retrieves metadata (entities and fields) from a specific external system. One implementation
/// per provider (Dataverse, Salesforce, ...). The owning service selects the right provider via
/// <see cref="SupportedType"/>.
/// </summary>
public interface IMetadataProvider
{
    /// <summary>The connection type this provider can serve metadata for.</summary>
    ConnectionType SupportedType { get; }

    /// <summary>Retrieves the list of entities (tables) exposed by the connection.</summary>
    Task<Result<List<EntityDto>>> GetEntitiesAsync(DatabaseConnection connection, CancellationToken cancellationToken = default);

    /// <summary>Retrieves the list of fields (columns) for a given entity.</summary>
    Task<Result<List<FieldDto>>> GetFieldsAsync(DatabaseConnection connection, string entityId, CancellationToken cancellationToken = default);
}
