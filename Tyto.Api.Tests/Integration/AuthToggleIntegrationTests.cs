using System.Net;
using FluentAssertions;

namespace Tyto.Api.Tests.Integration;

public class AuthToggleIntegrationTests
{
    [Fact]
    public async Task WhenAuthDisabled_ProtectedEndpoint_IsReachableAnonymously()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/language-models");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task WhenAuthEnabled_ProtectedEndpoint_RequiresAuthentication()
    {
        using var factory = new AuthEnabledWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/language-models");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task WhenAuthEnabled_HealthEndpoint_StaysAnonymous()
    {
        using var factory = new AuthEnabledWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
