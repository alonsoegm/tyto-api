using System.Diagnostics;
using Serilog.Context;

namespace Tyto.Api.Infrastructure.Middleware;

/// <summary>
/// Assigns a correlation id to every request so logs and the response can be tied together.
/// Uses a client-supplied <c>X-Correlation-ID</c> header when present, otherwise the current
/// trace id, falling back to a new GUID. The id is echoed back on the response and pushed into
/// the Serilog log context for the lifetime of the request.
/// </summary>
public class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-ID";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);

        context.Items[HeaderName] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var headerValue) &&
            !string.IsNullOrWhiteSpace(headerValue))
        {
            return headerValue.ToString();
        }

        return Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
    }
}
