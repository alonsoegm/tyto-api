using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Tyto.Api.Application.Common.Constants;

namespace Tyto.Api.Infrastructure.ExceptionHandlers;

public class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (statusCode, detail, code) = exception switch
        {
            ValidationException ve => (
                StatusCodes.Status400BadRequest,
                string.Join("; ", ve.Errors.Select(e => e.ErrorMessage)),
                ErrorCodes.ValidationError),
            _ => (
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred.",
                ErrorCodes.InternalError)
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unexpected exception occurred");
        else
            logger.LogWarning("Validation exception (fallback): {Message}", detail);

        httpContext.Response.StatusCode = statusCode;

        var ctx = new ProblemDetailsContext { HttpContext = httpContext, Exception = exception };
        ctx.ProblemDetails.Detail = detail;
        ctx.ProblemDetails.Extensions["code"] = code;

        return await problemDetailsService.TryWriteAsync(ctx);
    }
}
