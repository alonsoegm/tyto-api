using System.Reflection;
using Microsoft.OpenApi.Models;

namespace Tyto.Api.Extensions;

public static class SwaggerExtensions
{
    public static IServiceCollection AddSwaggerWithOAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "Tyto API", Version = "v1" });

            var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
            if (File.Exists(xmlPath))
                c.IncludeXmlComments(xmlPath);

            var tenantId = configuration["AzureAd:TenantId"];
            var clientId = configuration["AzureAd:ClientId"];
            var authBase = $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0";

            c.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.OAuth2,
                Flows = new OpenApiOAuthFlows
                {
                    AuthorizationCode = new OpenApiOAuthFlow
                    {
                        AuthorizationUrl = new Uri($"{authBase}/authorize"),
                        TokenUrl = new Uri($"{authBase}/token"),
                        Scopes = new Dictionary<string, string>
                        {
                            { $"api://{clientId}/access_as_user", "Access Tyto API" }
                        }
                    }
                }
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "oauth2" }
                    },
                    Array.Empty<string>()
                }
            });
        });

        return services;
    }

    public static WebApplication UseSwaggerWithOAuth(this WebApplication app, IConfiguration configuration)
    {
        // Fix: Chrome nulls window.opener after cross-origin navigation (login.microsoftonline.com → localhost).
        // This header explicitly restores that behavior for Swagger UI routes so oauth2-redirect.html can
        // call window.opener.swaggerUIRedirectOauth2(...) after the Azure AD callback.
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/swagger"))
                context.Response.Headers["Cross-Origin-Opener-Policy"] = "unsafe-none";
            await next();
        });

        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Tyto API v1");
            c.OAuthClientId(configuration["AzureAd:ClientId"]);
            c.OAuthUsePkce();
            c.OAuthScopeSeparator(" ");
            c.OAuthScopes($"api://{configuration["AzureAd:ClientId"]}/access_as_user");
        });

        return app;
    }
}
