using System.Net;
using FluentAssertions;

namespace Tyto.Api.Tests.Integration;

public class ApiResponseIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ApiResponseIntegrationTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetAll_ReturnsSuccessEnvelope()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/language-models");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("\"success\":true");
    }

    [Fact]
    public async Task GetById_WhenNotFound_ReturnsProblemDetailsWithNotFoundCode()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/language-models/{Guid.NewGuid()}");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        body.Should().Contain("NOT_FOUND");
    }
}
