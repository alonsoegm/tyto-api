using System.Net;
using System.Text;
using Tyto.Api.Application.Common.Errors;
using Tyto.Api.Application.Services.Metadata;
using Tyto.Api.Domain.Entities;
using Tyto.Api.Domain.Enums;
using FluentAssertions;
using FluentResults;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Tyto.Api.Tests.Services;

public class DataverseMetadataProviderTests
{
    private const string EnvironmentUrl = "https://org.crm.dynamics.com";

    [Fact]
    public async Task GetEntitiesAsync_ReturnsOnlyCustomEntities_MappingLabelWithFallback()
    {
        const string json = """
        {
          "value": [
            { "LogicalName": "new_invoice", "IsCustomEntity": true, "DisplayName": { "UserLocalizedLabel": { "Label": "Invoice" } } },
            { "LogicalName": "new_contract", "IsCustomEntity": true, "DisplayName": null },
            { "LogicalName": "account", "IsCustomEntity": false, "DisplayName": { "UserLocalizedLabel": { "Label": "Account" } } }
          ]
        }
        """;
        var (provider, handler) = CreateProvider(json);

        var result = await provider.GetEntitiesAsync(DataverseConnection());

        result.IsSuccess.Should().BeTrue();
        handler.CapturedUrl.Should().Contain($"{EnvironmentUrl}/api/data/v9.2/EntityDefinitions");
        result.Value.Should().BeEquivalentTo(new[]
        {
            new { Id = "new_invoice", Name = "Invoice" },
            new { Id = "new_contract", Name = "new_contract" } // fallback to logical name when no label
        });
        // The system entity (IsCustomEntity == false) is excluded.
        result.Value.Should().NotContain(e => e.Id == "account");
    }

    [Fact]
    public async Task GetFieldsAsync_MapsTypeAndQueriesEntityAttributes()
    {
        const string json = """
        {
          "value": [
            { "LogicalName": "name", "AttributeType": "String", "DisplayName": { "UserLocalizedLabel": { "Label": "Name" } } },
            { "LogicalName": "accountid", "AttributeType": "Lookup", "DisplayName": null }
          ]
        }
        """;
        var (provider, handler) = CreateProvider(json);

        var result = await provider.GetFieldsAsync(DataverseConnection(), "account");

        result.IsSuccess.Should().BeTrue();
        handler.CapturedUrl.Should().Contain("EntityDefinitions(LogicalName='account')/Attributes");
        result.Value.Should().BeEquivalentTo(new[]
        {
            new { Id = "name", Name = "Name", Type = "String" },
            new { Id = "accountid", Name = "accountid", Type = "Lookup" }
        });
    }

    [Fact]
    public async Task GetFieldsAsync_WhenEntityIdIsInvalid_ReturnsValidationError_WithoutCallingDataverse()
    {
        var (provider, handler) = CreateProvider("{ \"value\": [] }");

        var result = await provider.GetFieldsAsync(DataverseConnection(), "account'); drop");

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Should().BeOfType<ValidationError>();
        handler.CapturedUrl.Should().BeNull();
    }

    [Fact]
    public async Task GetEntitiesAsync_WhenAuthMethodIsNotClientSecret_FailsCleanly()
    {
        // Real provider (no token override) so the auth-method guard runs before any MSAL call.
        var provider = new DataverseMetadataProvider(
            DataProtection(), Mock.Of<IHttpClientFactory>(), NullLogger<DataverseMetadataProvider>.Instance);

        var connection = DataverseConnection();
        connection.DV_AuthMethod = DataverseAuthMethod.Certificate;

        var result = await provider.GetEntitiesAsync(connection);

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Should().BeOfType<ValidationError>();
    }

    private static DatabaseConnection DataverseConnection() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Dataverse",
        ConnectionType = ConnectionType.MsDataverse,
        DV_AuthMethod = DataverseAuthMethod.ClientSecret,
        DV_EnvironmentUrl = EnvironmentUrl,
        DV_TenantId = "tenant",
        DV_ClientId = "client",
        DV_ClientSecret = "encrypted-secret"
    };

    private static IDataProtectionProvider DataProtection()
    {
        var protector = new Mock<IDataProtector>();
        protector.Setup(p => p.Unprotect(It.IsAny<byte[]>())).Returns((byte[] b) => b);
        var provider = new Mock<IDataProtectionProvider>();
        provider.Setup(p => p.CreateProtector(It.IsAny<string>())).Returns(protector.Object);
        return provider.Object;
    }

    private static (TestableProvider Provider, StubHandler Handler) CreateProvider(string responseJson)
    {
        var handler = new StubHandler(responseJson);
        var httpClient = new HttpClient(handler);

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var provider = new TestableProvider(
            DataProtection(), httpClientFactory.Object, NullLogger<DataverseMetadataProvider>.Instance);

        return (provider, handler);
    }

    /// <summary>Bypasses MSAL token acquisition so the HTTP/mapping path can be tested in isolation.</summary>
    private sealed class TestableProvider : DataverseMetadataProvider
    {
        public TestableProvider(IDataProtectionProvider dp, IHttpClientFactory factory, ILogger<DataverseMetadataProvider> logger)
            : base(dp, factory, logger)
        {
        }

        protected override Task<Result<string>> AcquireTokenAsync(DatabaseConnection connection, CancellationToken cancellationToken)
            => Task.FromResult(Result.Ok("fake-token"));
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _responseJson;

        public StubHandler(string responseJson) => _responseJson = responseJson;

        public string? CapturedUrl { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CapturedUrl = request.RequestUri?.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseJson, Encoding.UTF8, "application/json")
            });
        }
    }
}
