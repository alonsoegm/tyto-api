using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Tyto.Api.Infrastructure.Data;

public class TytoDbContextFactory : IDesignTimeDbContextFactory<TytoDbContext>
{
    public TytoDbContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddUserSecrets(typeof(TytoDbContextFactory).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<TytoDbContext>();
        optionsBuilder.UseSqlServer(configuration.GetConnectionString("TytoDb"));

        return new TytoDbContext(optionsBuilder.Options);
    }
}
