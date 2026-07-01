using FluentResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Tyto.Api.Application.Common.Errors;
using Tyto.Api.Application.DTOs.Metadata;
using Tyto.Api.Application.Interfaces;
using Tyto.Api.Domain.Entities;
using Tyto.Api.Infrastructure.Data;

namespace Tyto.Api.Application.Services;

/// <inheritdoc />
public class MetadataService : IMetadataService
{
    // Entities are effectively static schema data and the external endpoints are slow, so the full
    // list is cached in memory per connection for a fixed window. Fields are intentionally not cached.
    private const string EntitiesCacheKeyPrefix = "dataverse:entities:";
    private static readonly TimeSpan EntitiesCacheDuration = TimeSpan.FromMinutes(30);

    private readonly TytoDbContext _db;
    private readonly IEnumerable<IMetadataProvider> _providers;
    private readonly IMemoryCache _cache;

    public MetadataService(TytoDbContext db, IEnumerable<IMetadataProvider> providers, IMemoryCache cache)
    {
        _db = db;
        _providers = providers;
        _cache = cache;
    }

    /// <inheritdoc />
    public async Task<Result<List<EntityDto>>> GetEntitiesAsync(Guid databaseConnectionId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{EntitiesCacheKeyPrefix}{databaseConnectionId}";
        if (_cache.TryGetValue(cacheKey, out List<EntityDto>? cachedEntities) && cachedEntities is not null)
            return Result.Ok(cachedEntities);

        var resolution = await ResolveProviderAsync(databaseConnectionId, cancellationToken);
        if (resolution.IsFailed)
            return Result.Fail<List<EntityDto>>(resolution.Errors);

        var (connection, provider) = resolution.Value;
        var result = await provider.GetEntitiesAsync(connection, cancellationToken);

        // Only cache successful results so a transient failure isn't pinned for the whole window.
        if (result.IsSuccess)
            _cache.Set(cacheKey, result.Value, EntitiesCacheDuration);

        return result;
    }

    /// <inheritdoc />
    public async Task<Result<List<FieldDto>>> GetFieldsAsync(Guid databaseConnectionId, string entityId, CancellationToken cancellationToken = default)
    {
        var resolution = await ResolveProviderAsync(databaseConnectionId, cancellationToken);
        if (resolution.IsFailed)
            return Result.Fail<List<FieldDto>>(resolution.Errors);

        var (connection, provider) = resolution.Value;
        return await provider.GetFieldsAsync(connection, entityId, cancellationToken);
    }

    private async Task<Result<ProviderContext>> ResolveProviderAsync(Guid databaseConnectionId, CancellationToken cancellationToken)
    {
        var connection = await _db.DatabaseConnections
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == databaseConnectionId, cancellationToken);

        if (connection is null)
            return Result.Fail<ProviderContext>(new NotFoundError(nameof(DatabaseConnection), databaseConnectionId));

        var provider = _providers.FirstOrDefault(p => p.SupportedType == connection.ConnectionType);
        if (provider is null)
            return Result.Fail<ProviderContext>(new InternalError(
                $"No metadata provider is available for connection type '{connection.ConnectionType}'."));

        return Result.Ok(new ProviderContext(connection, provider));
    }

    private sealed record ProviderContext(DatabaseConnection Connection, IMetadataProvider Provider);
}
