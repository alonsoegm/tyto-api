using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Tyto.Api.Infrastructure.Data;

public class TytoDbContextFactory : IDesignTimeDbContextFactory<TytoDbContext>
{
    public TytoDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<TytoDbContext>();
        optionsBuilder.UseNpgsql(configuration.GetConnectionString("TytoDb"));

        return new TytoDbContext(optionsBuilder.Options);
    }
}
