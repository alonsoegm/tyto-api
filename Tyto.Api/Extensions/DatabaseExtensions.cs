using Microsoft.EntityFrameworkCore;
using Tyto.Api.Infrastructure.Data;

namespace Tyto.Api.Extensions;

public static class DatabaseExtensions
{
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<TytoDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("TytoDb"),
                sqlOptions => sqlOptions.EnableRetryOnFailure()));

        return services;
    }
}
