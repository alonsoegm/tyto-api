using Tyto.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Tyto.Api.Extensions;

public static class DatabaseExtensions
{
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<TytoDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("TytoDb")));

        return services;
    }
}
