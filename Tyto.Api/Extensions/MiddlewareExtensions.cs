using Tyto.Api.Infrastructure.Middleware;

namespace Tyto.Api.Extensions;

public static class MiddlewareExtensions
{
    public static WebApplication UseUnitOfWork(this WebApplication app)
    {
        app.UseMiddleware<UnitOfWorkMiddleware>();
        return app;
    }

    public static WebApplication UseCorrelationId(this WebApplication app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        return app;
    }
}
