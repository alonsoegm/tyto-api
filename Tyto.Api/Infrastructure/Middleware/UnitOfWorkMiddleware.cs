using Tyto.Api.Application.Common.Constants;
using Tyto.Api.Application.Interfaces;

namespace Tyto.Api.Infrastructure.Middleware;

/// <summary>
/// Middleware that defers all database commits until the end of request processing.
/// Ensures atomic transactions by calling SaveChangesAsync once after successful controller execution.
/// </summary>
public class UnitOfWorkMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<UnitOfWorkMiddleware> _logger;

    public UnitOfWorkMiddleware(RequestDelegate next, ILogger<UnitOfWorkMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IUnitOfWork unitOfWork, IProblemDetailsService problemDetailsService)
    {
        // Execute the request pipeline (controllers, services, etc.)
        await _next(context);

        // Only commit if the response is successful (2xx status code)
        if (context.Response.StatusCode >= 200 && context.Response.StatusCode < 300)
        {
            // Optimization: Only commit if there are actual changes
            if (unitOfWork.HasChanges())
            {
                try
                {
                    var changeCount = await unitOfWork.CommitAsync(context.RequestAborted);
                    _logger.LogDebug("Unit of Work committed {ChangeCount} changes for request {Method} {Path}",
                        changeCount, context.Request.Method, context.Request.Path);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to commit Unit of Work for request {Method} {Path}",
                        context.Request.Method, context.Request.Path);

                    context.Response.Clear();
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;

                    var ctx = new ProblemDetailsContext { HttpContext = context, Exception = ex };
                    ctx.ProblemDetails.Detail = "Failed to commit database changes. The operation has been rolled back.";
                    ctx.ProblemDetails.Extensions["code"] = ErrorCodes.InternalError;
                    await problemDetailsService.TryWriteAsync(ctx);
                }
            }
        }
        else
        {
            // Non-success response: changes will be automatically discarded when DbContext is disposed
            if (unitOfWork.HasChanges())
            {
                _logger.LogDebug("Unit of Work rollback: pending changes discarded due to status code {StatusCode}",
                    context.Response.StatusCode);
            }
        }
    }
}
