using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace Tyto.Api.Extensions;

public static class HealthCheckExtensions
{
    private const string ReadyTag = "ready";

    /// <summary>
    /// Registers application health checks, including a PostgreSQL readiness probe.
    /// </summary>
    public static IServiceCollection AddHealthChecksConfig(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("TytoDb") ?? string.Empty;

        services.AddHealthChecks()
            .AddNpgSql(connectionString, name: "postgres", tags: [ReadyTag]);

        return services;
    }

    /// <summary>
    /// Maps the liveness and readiness health check endpoints. Both are anonymous so that
    /// orchestrators (Kubernetes, Container Apps) can probe them without a token.
    /// </summary>
    public static WebApplication MapHealthCheckEndpoints(this WebApplication app)
    {
        // Liveness: confirms the process is up and responding, without running any checks.
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false
        }).AllowAnonymous();

        // Readiness: runs the checks tagged "ready" (e.g. database connectivity).
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(ReadyTag)
        }).AllowAnonymous();

        return app;
    }
}
