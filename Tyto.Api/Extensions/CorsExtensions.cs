namespace Tyto.Api.Extensions;

public static class CorsExtensions
{
    private const string PolicyName = "TytoCors";

    public static IServiceCollection AddCorsPolicy(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        services.AddCors(options =>
        {
            options.AddPolicy(PolicyName, policy =>
            {
                if (allowedOrigins.Length > 0)
                    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
                else if (environment.IsDevelopment())
                    // No origins configured: allow any origin only in local development.
                    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
                // Outside Development with no configured origins the policy stays empty (closed):
                // cross-origin requests are blocked until Cors:AllowedOrigins is set.
            });
        });

        return services;
    }

    public static WebApplication UseCorsPolicy(this WebApplication app)
    {
        app.UseCors(PolicyName);
        return app;
    }
}
