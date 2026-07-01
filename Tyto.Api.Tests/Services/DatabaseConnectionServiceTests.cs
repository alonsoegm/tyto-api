using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Tyto.Api.Application.Common.Errors;
using Tyto.Api.Application.DTOs.DatabaseConnection;
using Tyto.Api.Application.Services;
using Tyto.Api.Domain.Configs;
using Tyto.Api.Domain.Entities;
using Tyto.Api.Domain.Enums;
using Tyto.Api.Infrastructure.Data;
using Tyto.Api.Tests.Infrastructure;
using Tyto.Api.Validators.DatabaseConnection;

namespace Tyto.Api.Tests.Services;

/// <summary>
/// Covers the refactored generic Config persistence model, secret protection, and the server-side
/// rules protecting the system-managed internal connection.
/// </summary>
public class DatabaseConnectionServiceTests
{
    private const string SecretPlaceholder = "••••••••••••••";

    // ----- Seed / persistence --------------------------------------------------------------------

    [Fact]
    public void InternalConnection_IsSeeded_AndHasNoConfigPayload()
    {
        using var db = TestDbContextFactory.Create();
        db.Database.EnsureCreated();

        var seeded = db.DatabaseConnections.Single(x => x.IsInternal);

        seeded.Id.Should().Be(DatabaseConnection.InternalConnectionId);
        seeded.Name.Should().Be(DatabaseConnection.InternalConnectionName);
        seeded.ConnectionType.Should().Be(ConnectionType.InternalSql);
        seeded.Config.Should().BeNull();
        seeded.CreatedBy.Should().Be("system");
    }

    [Fact]
    public async Task DataverseConnection_RoundTripsConfigPayload()
    {
        using var db = TestDbContextFactory.Create();
        db.DatabaseConnections.Add(new DatabaseConnection
        {
            Name = "DV",
            ConnectionType = ConnectionType.Dataverse,
            Config = ConnectionConfigSerializer.Serialize(new DataverseConfig
            {
                AuthMethod = DataverseAuthMethod.ClientSecret,
                EnvironmentUrl = "https://org.crm.dynamics.com",
                TenantId = "tenant",
                ClientId = "client",
                ClientSecret = "encrypted"
            })
        });
        await db.SaveChangesAsync();

        var stored = await db.DatabaseConnections.SingleAsync(x => x.ConnectionType == ConnectionType.Dataverse);
        var parsed = stored.GetDataverseConfig();

        parsed.Should().NotBeNull();
        parsed!.EnvironmentUrl.Should().Be("https://org.crm.dynamics.com");
        parsed.TenantId.Should().Be("tenant");
    }

    [Fact]
    public void DatabaseConnectionEntity_HasNoConnectionTypeSpecificProperties()
    {
        var propertyNames = typeof(DatabaseConnection).GetProperties().Select(p => p.Name).ToList();

        propertyNames.Should().NotContain(n => n.StartsWith("SF_", StringComparison.Ordinal));
        propertyNames.Should().NotContain(n => n.StartsWith("DV_", StringComparison.Ordinal));
        propertyNames.Should().Contain(new[] { "IsInternal", "Config" });
    }

    // ----- Create --------------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_Salesforce_EncryptsSecretAndMasksItInResponse()
    {
        using var db = TestDbContextFactory.Create();
        var svc = CreateService(db);

        var result = await svc.CreateAsync(
            new DatabaseConnectionCreateDto("SF", "desc", ConnectionType.Salesforce, SalesforceConfig("topsecret")),
            "tester");

        result.IsSuccess.Should().BeTrue();
        result.Value.IsInternal.Should().BeFalse();
        result.Value.Config!["clientSecret"]!.GetValue<string>().Should().Be(SecretPlaceholder);

        var stored = await db.DatabaseConnections.FindAsync(result.Value.Id);
        stored!.Config.Should().NotContain("topsecret", "the secret must be encrypted at rest");
    }

    [Theory]
    [InlineData(ConnectionType.InternalSql)]
    [InlineData(ConnectionType.CosmosDb)]
    public async Task CreateAsync_RejectsNonCreatableConnectionTypes(ConnectionType type)
    {
        using var db = TestDbContextFactory.Create();
        var svc = CreateService(db);

        var result = await svc.CreateAsync(new DatabaseConnectionCreateDto("X", "", type, null), "tester");

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Should().BeOfType<ValidationError>();
        (await db.DatabaseConnections.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CreateAsync_RejectsDuplicateName()
    {
        using var db = TestDbContextFactory.Create();
        var svc = CreateService(db);

        (await svc.CreateAsync(new DatabaseConnectionCreateDto("dupe", "", ConnectionType.Salesforce, SalesforceConfig("s")), "t"))
            .IsSuccess.Should().BeTrue();
        var second = await svc.CreateAsync(
            new DatabaseConnectionCreateDto("dupe", "", ConnectionType.Salesforce, SalesforceConfig("s")), "t");

        second.IsFailed.Should().BeTrue();
        second.Errors.Should().ContainSingle().Which.Should().BeOfType<ConflictError>();
    }

    // ----- Secret preservation on update ----------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_WithPlaceholderSecret_PreservesStoredSecret()
    {
        using var db = TestDbContextFactory.Create();
        var svc = CreateService(db);

        var created = await svc.CreateAsync(
            new DatabaseConnectionCreateDto("SF", "", ConnectionType.Salesforce, SalesforceConfig("original-secret")), "t");
        var encryptedBefore = (await db.DatabaseConnections.FindAsync(created.Value.Id))!.GetSalesforceConfig()!.ClientSecret;

        // Client re-sends the masked placeholder — the stored secret must be preserved untouched.
        var update = await svc.UpdateAsync(created.Value.Id,
            new DatabaseConnectionUpdateDto("SF", "changed", SalesforceConfig(SecretPlaceholder)), "t");

        update.IsSuccess.Should().BeTrue();
        var storedAfter = (await db.DatabaseConnections.FindAsync(created.Value.Id))!;
        storedAfter.Description.Should().Be("changed");
        storedAfter.GetSalesforceConfig()!.ClientSecret.Should().Be(encryptedBefore);
        storedAfter.Config.Should().NotContain("original-secret");
        storedAfter.Config.Should().NotContain(SecretPlaceholder, "the placeholder must never be persisted");
    }

    // ----- Internal connection protection --------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_OnInternalConnection_IsForbidden()
    {
        using var db = TestDbContextFactory.Create();
        db.Database.EnsureCreated();
        var svc = CreateService(db);

        var result = await svc.UpdateAsync(DatabaseConnection.InternalConnectionId,
            new DatabaseConnectionUpdateDto("hacked", "", null), "t");

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Should().BeOfType<ForbiddenError>();
    }

    [Fact]
    public async Task DeleteAsync_OnInternalConnection_IsForbidden()
    {
        using var db = TestDbContextFactory.Create();
        db.Database.EnsureCreated();
        var svc = CreateService(db);

        var result = await svc.DeleteAsync(DatabaseConnection.InternalConnectionId, "t");

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Should().BeOfType<ForbiddenError>();
    }

    [Fact]
    public async Task TestConnectionAsync_ForInternalSql_IsForbidden()
    {
        using var db = TestDbContextFactory.Create();
        var svc = CreateService(db);

        var result = await svc.TestConnectionAsync(new TestDatabaseConnectionDto(ConnectionType.InternalSql, null));

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Should().BeOfType<ForbiddenError>();
    }

    [Fact]
    public async Task TestConnectionAsync_ForCosmosDb_ReturnsComingSoonResult()
    {
        using var db = TestDbContextFactory.Create();
        var svc = CreateService(db);

        var result = await svc.TestConnectionAsync(new TestDatabaseConnectionDto(ConnectionType.CosmosDb, null));

        result.IsSuccess.Should().BeTrue();
        result.Value.IsSuccess.Should().BeFalse();
        result.Value.Message.Should().Contain("coming soon");
    }

    // ----- Helpers -------------------------------------------------------------------------------

    private static DatabaseConnectionService CreateService(TytoDbContext db) =>
        new(
            db,
            new AuditLogService(db),
            new EphemeralDataProtectionProvider(),
            new DatabaseConnectionCreateValidator(),
            new DatabaseConnectionUpdateValidator(),
            new TestDatabaseConnectionValidator(),
            new SalesforceConfigValidator(),
            new DataverseConfigValidator(),
            NullLogger<DatabaseConnectionService>.Instance,
            Mock.Of<IHttpClientFactory>());

    private static JsonObject SalesforceConfig(string clientSecret) =>
        JsonSerializer.SerializeToNode(new SalesforceConfig
        {
            AuthMethod = SalesforceAuthMethod.ClientCredentials,
            InstanceUrl = "https://x.my.salesforce.com",
            ConsumerKey = "ck",
            ClientSecret = clientSecret
        }, ConnectionConfigSerializer.Options)!.AsObject();
}
