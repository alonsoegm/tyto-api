using System.Net;
using FluentAssertions;

namespace Tyto.Api.Tests.Integration;

public class HealthCheckIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public HealthCheckIntegrationTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task LivenessProbe_IsAnonymous_AndReturnsHealthy()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
