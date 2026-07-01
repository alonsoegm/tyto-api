using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tyto.Api.Domain.Entities;
using Tyto.Api.Domain.Enums;
using Tyto.Api.Infrastructure.Data;

namespace Tyto.Api.Tests.Integration;

/// <summary>
/// Exercises the Database Connections API contract end-to-end: exposing IsInternal, returning the
/// seeded internal connection, masking secrets, and the server-side rules protecting internal
/// connections and blocking non-creatable types.
/// </summary>
public class DatabaseConnectionsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string SecretPlaceholder = "••••••••••••••";
    private readonly CustomWebApplicationFactory _factory;

    public DatabaseConnectionsControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        EnsureInternalConnectionSeeded();
    }

    [Fact]
    public async Task GetAll_ReturnsSeededInternalConnection_WithIsInternalAndNullConfig()
    {
        var client = _factory.CreateClient();

        var doc = await GetJsonAsync(client, "/api/database-connections");
        var items = doc.RootElement.GetProperty("data").GetProperty("items");

        var internalConn = items.EnumerateArray()
            .Single(x => x.GetProperty("isInternal").GetBoolean());
        internalConn.GetProperty("name").GetString().Should().Be("Tyto Internal");
        internalConn.GetProperty("connectionType").GetString().Should().Be("InternalSql");
        internalConn.GetProperty("config").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task GetById_SeededInternalConnection_IsReturned()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/database-connections/{DatabaseConnection.InternalConnectionId}");
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        doc.RootElement.GetProperty("data").GetProperty("isInternal").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Create_Salesforce_Returns201_MasksSecret_AndNeverReturnsPlaintext()
    {
        var client = _factory.CreateClient();
        var payload = new
        {
            name = $"SF-{Guid.NewGuid():N}",
            description = "sf",
            connectionType = "Salesforce",
            config = new
            {
                authMethod = "ClientCredentials",
                instanceUrl = "https://x.my.salesforce.com",
                consumerKey = "ck",
                clientSecret = "topsecret-value"
            }
        };

        var response = await client.PostAsJsonAsync("/api/database-connections", payload);
        var rawBody = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(rawBody);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("isInternal").GetBoolean().Should().BeFalse();
        data.GetProperty("config").GetProperty("clientSecret").GetString().Should().Be(SecretPlaceholder);
        rawBody.Should().NotContain("topsecret-value", "the plaintext secret must never be returned");
    }

    [Fact]
    public async Task Create_Dataverse_Returns201()
    {
        var client = _factory.CreateClient();
        // Managed Identity avoids any outbound token/network call during the post-create auto-test.
        var payload = new
        {
            name = $"DV-{Guid.NewGuid():N}",
            description = "dv",
            connectionType = "Dataverse",
            config = new
            {
                authMethod = "ManagedIdentity",
                environmentUrl = "https://org.crm.dynamics.com",
                tenantId = "tenant",
                clientId = "client",
                managedIdentityType = "SystemAssigned"
            }
        };

        var response = await client.PostAsJsonAsync("/api/database-connections", payload);
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        doc.RootElement.GetProperty("data").GetProperty("connectionType").GetString().Should().Be("Dataverse");
    }

    [Theory]
    [InlineData("InternalSql")]
    [InlineData("CosmosDb")]
    public async Task Create_NonCreatableType_ReturnsValidationError(string connectionType)
    {
        var client = _factory.CreateClient();
        var payload = new { name = $"X-{Guid.NewGuid():N}", description = "", connectionType, config = (object?)null };

        var response = await client.PostAsJsonAsync("/api/database-connections", payload);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Should().Contain("VALIDATION_ERROR");
    }

    [Fact]
    public async Task Update_InternalConnection_ReturnsForbidden()
    {
        var client = _factory.CreateClient();
        var payload = new { name = "hacked", description = "", config = (object?)null };

        var response = await client.PutAsJsonAsync(
            $"/api/database-connections/{DatabaseConnection.InternalConnectionId}", payload);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        body.Should().Contain("FORBIDDEN");
    }

    [Fact]
    public async Task Delete_InternalConnection_ReturnsForbidden()
    {
        var client = _factory.CreateClient();

        var response = await client.DeleteAsync(
            $"/api/database-connections/{DatabaseConnection.InternalConnectionId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TestConnection_InternalSql_ReturnsForbidden()
    {
        var client = _factory.CreateClient();
        var payload = new { connectionType = "InternalSql", config = (object?)null };

        var response = await client.PostAsJsonAsync("/api/database-connections/test-connection", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task<JsonDocument> GetJsonAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private void EnsureInternalConnectionSeeded()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TytoDbContext>();
        db.Database.EnsureCreated();

        if (db.DatabaseConnections.Any(x => x.IsInternal))
            return;

        db.DatabaseConnections.Add(new DatabaseConnection
        {
            Id = DatabaseConnection.InternalConnectionId,
            Name = DatabaseConnection.InternalConnectionName,
            ConnectionType = ConnectionType.InternalSql,
            IsInternal = true,
            Config = null,
            CreatedBy = "system",
            UpdatedBy = "system"
        });
        db.SaveChanges();
    }
}
