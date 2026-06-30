using System.Net;
using System.Text;
using FluentAssertions;

namespace Tyto.Api.Tests.Integration;

public class RateLimitingIntegrationTests
{
    // Each test uses its own factory instance so the in-process rate limiter starts with a
    // fresh permit window (limiter state lives in the host, not in the partition key alone).

    private static StringContent EmptyBody() =>
        new("{}", Encoding.UTF8, "application/json");

    [Fact]
    public async Task TestConnectionEndpoint_BlocksRequestsBeyondTheLimit()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 6; i++)
        {
            // Empty body fails validation (400) without any outbound call, but the rate limiter
            // still consumes a permit per request since it runs before the controller executes.
            var response = await client.PostAsync("/api/language-models/test-connection", EmptyBody());
            statuses.Add(response.StatusCode);
        }

        statuses.Take(5).Should().NotContain(HttpStatusCode.TooManyRequests);
        statuses[5].Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task TestConnectionEndpoint_RejectionBody_UsesProblemDetailsCode()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        HttpResponseMessage? rejected = null;
        for (var i = 0; i < 7 && rejected is null; i++)
        {
            var response = await client.PostAsync("/api/language-models/test-connection", EmptyBody());
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                rejected = response;
        }

        rejected.Should().NotBeNull("the limiter should reject once the permit window is exhausted");
        var body = await rejected!.Content.ReadAsStringAsync();
        body.Should().Contain("RATE_LIMIT_EXCEEDED");
    }
}
