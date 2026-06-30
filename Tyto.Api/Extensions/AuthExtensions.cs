using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web;

namespace Tyto.Api.Extensions;

public static class AuthExtensions
{
    /// <summary>
    /// Registers Azure AD JWT authentication and a global fallback authorization policy.
    /// The fallback policy is controlled by the <c>Authentication:Enabled</c> flag: when enabled,
    /// every endpoint requires an authenticated user; when disabled (e.g. local development),
    /// all endpoints are reachable anonymously without per-controller <c>[Authorize]</c> attributes.
    /// </summary>
    public static IServiceCollection AddAzureAdAuth(this IServiceCollection services, IConfiguration configuration)
    {
        // The JWT bearer scheme is always registered so tokens are validated whenever present.
        services.AddMicrosoftIdentityWebApiAuthentication(configuration, "AzureAd");

        var authEnabled = configuration.GetValue<bool>("Authentication:Enabled");

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(BuildFallbackPolicy(authEnabled));

        return services;
    }

    private static AuthorizationPolicy BuildFallbackPolicy(bool authEnabled)
    {
        var builder = new AuthorizationPolicyBuilder();

        if (authEnabled)
            builder.RequireAuthenticatedUser();
        else
            builder.RequireAssertion(_ => true);

        return builder.Build();
    }
}
