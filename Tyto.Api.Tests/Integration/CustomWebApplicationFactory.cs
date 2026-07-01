using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tyto.Api.Infrastructure.Data;

namespace Tyto.Api.Tests.Integration;

/// <summary>
/// Boots the real application pipeline in-memory for integration tests, swapping the
/// PostgreSQL <see cref="TytoDbContext"/> for an EF Core in-memory database so no
/// external infrastructure is required. The Azure AD authentication toggle can be flipped
/// per factory instance to exercise both the auth-on and auth-off paths.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"IntegrationTests-{Guid.NewGuid()}";

    /// <summary>Whether the global authentication requirement is enabled. Off by default.</summary>
    protected virtual bool AuthEnabled => false;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // UseSetting feeds the builder configuration early enough that Program.cs sees it when it
        // reads "Authentication:Enabled" during service registration (before the host is built).
        builder.UseSetting("Authentication:Enabled", AuthEnabled ? "true" : "false");

        builder.ConfigureServices(services =>
        {
            RemoveDbContextRegistrations(services);

            services.AddDbContext<TytoDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
        });
    }

    private static void RemoveDbContextRegistrations(IServiceCollection services)
    {
        var descriptorsToRemove = services
            .Where(d =>
                d.ServiceType == typeof(DbContextOptions<TytoDbContext>) ||
                d.ServiceType == typeof(DbContextOptions) ||
                d.ServiceType == typeof(TytoDbContext) ||
                // EF Core 9+ also registers an IDbContextOptionsConfiguration<TContext> that
                // re-applies the Npgsql provider; match it by name to stay version-independent.
                d.ServiceType.FullName?.Contains("DbContextOptionsConfiguration", StringComparison.Ordinal) == true)
            .ToList();

        foreach (var descriptor in descriptorsToRemove)
            services.Remove(descriptor);
    }
}

/// <summary>Variant of <see cref="CustomWebApplicationFactory"/> with the auth requirement enabled.</summary>
public class AuthEnabledWebApplicationFactory : CustomWebApplicationFactory
{
    protected override bool AuthEnabled => true;
}
