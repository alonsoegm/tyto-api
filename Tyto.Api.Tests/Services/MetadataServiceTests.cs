using Tyto.Api.Application.Common.Errors;
using Tyto.Api.Application.DTOs.Metadata;
using Tyto.Api.Application.Interfaces;
using Tyto.Api.Application.Services;
using Tyto.Api.Domain.Entities;
using Tyto.Api.Domain.Enums;
using Tyto.Api.Infrastructure.Data;
using Tyto.Api.Tests.Infrastructure;
using FluentAssertions;
using FluentResults;
using Microsoft.Extensions.Caching.Memory;

namespace Tyto.Api.Tests.Services;

public class MetadataServiceTests
{
    [Fact]
    public async Task GetEntitiesAsync_WhenConnectionDoesNotExist_ReturnsNotFound()
    {
        using var db = TestDbContextFactory.Create();
        var service = CreateService(db, new StubProvider(ConnectionType.MsDataverse));

        var result = await service.GetEntitiesAsync(Guid.NewGuid());

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Should().BeOfType<NotFoundError>();
    }

    [Fact]
    public async Task GetEntitiesAsync_SelectsProviderMatchingConnectionType()
    {
        using var db = TestDbContextFactory.Create();
        var connection = await SeedConnectionAsync(db, ConnectionType.MsDataverse);

        var expected = new List<EntityDto> { new("account", "Account") };
        var dataverseProvider = new StubProvider(ConnectionType.MsDataverse, expected);
        var salesforceProvider = new StubProvider(ConnectionType.Salesforce, new List<EntityDto> { new("Lead", "Lead") });

        var service = CreateService(db, salesforceProvider, dataverseProvider);

        var result = await service.GetEntitiesAsync(connection.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GetEntitiesAsync_OnSecondCall_ReturnsCachedResultWithoutHittingProvider()
    {
        using var db = TestDbContextFactory.Create();
        var connection = await SeedConnectionAsync(db, ConnectionType.MsDataverse);

        var provider = new StubProvider(ConnectionType.MsDataverse, new List<EntityDto> { new("account", "Account") });
        var service = CreateService(db, provider);

        var first = await service.GetEntitiesAsync(connection.Id);
        var second = await service.GetEntitiesAsync(connection.Id);

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        second.Value.Should().BeEquivalentTo(first.Value);
        provider.GetEntitiesCallCount.Should().Be(1, "the second call should be served from cache");
    }

    [Fact]
    public async Task GetEntitiesAsync_CachesPerConnection()
    {
        using var db = TestDbContextFactory.Create();
        var connectionA = await SeedConnectionAsync(db, ConnectionType.MsDataverse);
        var connectionB = await SeedConnectionAsync(db, ConnectionType.MsDataverse);

        var provider = new StubProvider(ConnectionType.MsDataverse, new List<EntityDto> { new("account", "Account") });
        var service = CreateService(db, provider);

        await service.GetEntitiesAsync(connectionA.Id);
        await service.GetEntitiesAsync(connectionB.Id);

        provider.GetEntitiesCallCount.Should().Be(2, "each connection has its own cache entry");
    }

    [Fact]
    public async Task GetEntitiesAsync_WhenProviderFails_DoesNotCacheResult()
    {
        using var db = TestDbContextFactory.Create();
        var connection = await SeedConnectionAsync(db, ConnectionType.MsDataverse);

        var provider = new StubProvider(ConnectionType.MsDataverse) { FailEntities = true };
        var service = CreateService(db, provider);

        var first = await service.GetEntitiesAsync(connection.Id);
        var second = await service.GetEntitiesAsync(connection.Id);

        first.IsFailed.Should().BeTrue();
        second.IsFailed.Should().BeTrue();
        provider.GetEntitiesCallCount.Should().Be(2, "a failed fetch should not be cached");
    }

    [Fact]
    public async Task GetFieldsAsync_DelegatesToMatchingProvider()
    {
        using var db = TestDbContextFactory.Create();
        var connection = await SeedConnectionAsync(db, ConnectionType.MsDataverse);

        var provider = new StubProvider(ConnectionType.MsDataverse);
        var service = CreateService(db, provider);

        var result = await service.GetFieldsAsync(connection.Id, "account");

        result.IsSuccess.Should().BeTrue();
        provider.LastEntityId.Should().Be("account");
    }

    [Fact]
    public async Task GetEntitiesAsync_WhenNoProviderSupportsConnectionType_ReturnsInternalError()
    {
        using var db = TestDbContextFactory.Create();
        var connection = await SeedConnectionAsync(db, ConnectionType.Salesforce);

        // Only a Dataverse provider is registered, but the connection is Salesforce.
        var service = CreateService(db, new StubProvider(ConnectionType.MsDataverse));

        var result = await service.GetEntitiesAsync(connection.Id);

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Should().BeOfType<InternalError>();
    }

    private static MetadataService CreateService(TytoDbContext db, params IMetadataProvider[] providers)
        => new(db, providers, new MemoryCache(new MemoryCacheOptions()));

    private static async Task<DatabaseConnection> SeedConnectionAsync(TytoDbContext db, ConnectionType type)
    {
        var connection = new DatabaseConnection
        {
            Id = Guid.NewGuid(),
            Name = $"Test {type}",
            ConnectionType = type
        };
        db.DatabaseConnections.Add(connection);
        await db.SaveChangesAsync();
        return connection;
    }

    private sealed class StubProvider : IMetadataProvider
    {
        private readonly List<EntityDto> _entities;

        public StubProvider(ConnectionType supportedType, List<EntityDto>? entities = null)
        {
            SupportedType = supportedType;
            _entities = entities ?? new List<EntityDto>();
        }

        public ConnectionType SupportedType { get; }

        public string? LastEntityId { get; private set; }

        public int GetEntitiesCallCount { get; private set; }

        public bool FailEntities { get; init; }

        public Task<Result<List<EntityDto>>> GetEntitiesAsync(DatabaseConnection connection, CancellationToken cancellationToken = default)
        {
            GetEntitiesCallCount++;
            return Task.FromResult(FailEntities
                ? Result.Fail<List<EntityDto>>(new InternalError("Simulated provider failure."))
                : Result.Ok(_entities));
        }

        public Task<Result<List<FieldDto>>> GetFieldsAsync(DatabaseConnection connection, string entityId, CancellationToken cancellationToken = default)
        {
            LastEntityId = entityId;
            return Task.FromResult(Result.Ok(new List<FieldDto>()));
        }
    }
}
