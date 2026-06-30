using Tyto.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Tyto.Api.Tests.Infrastructure;

/// <summary>
/// Creates isolated in-memory <see cref="TytoDbContext"/> instances for unit tests.
/// Each call uses a unique database name so tests do not share state.
/// </summary>
internal static class TestDbContextFactory
{
    public static TytoDbContext Create()
    {
        var options = new DbContextOptionsBuilder<TytoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .EnableSensitiveDataLogging()
            .Options;

        return new TytoDbContext(options);
    }
}
