using System.Threading.RateLimiting;
using Tyto.Api.Application.Common.Constants;
using Microsoft.AspNetCore.RateLimiting;

namespace Tyto.Api.Extensions;

public static class RateLimitingExtensions
{
    /// <summary>
    /// Name of the rate limiting policy applied to the expensive "test-connection" endpoints.
    /// </summary>
    public const string TestConnectionPolicy = "TestConnectionPolicy";

    private const int PermitLimit = 5;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Registers rate limiting with a fixed-window policy for the "test-connection" endpoints,
    /// which trigger costly outbound calls (Azure OpenAI/Foundry, CRM databases). Requests are
    /// partitioned by authenticated user (object id claim) or by client IP when anonymous.
    /// </summary>
    public static IServiceCollection AddRateLimitingConfig(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(TestConnectionPolicy, httpContext =>
            {
                var partitionKey = httpContext.User.FindFirst(AppClaimTypes.ObjectId)?.Value
                    ?? httpContext.Connection.RemoteIpAddress?.ToString()
                    ?? "anonymous";

                return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = PermitLimit,
                    Window = Window,
                    QueueLimit = 0,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                });
            });

            options.OnRejected = async (context, cancellationToken) =>
            {
                var problemDetailsService = context.HttpContext.RequestServices
                    .GetRequiredService<IProblemDetailsService>();

                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                var problemContext = new ProblemDetailsContext { HttpContext = context.HttpContext };
                problemContext.ProblemDetails.Status = StatusCodes.Status429TooManyRequests;
                problemContext.ProblemDetails.Title = "Too many requests";
                problemContext.ProblemDetails.Detail =
                    "Too many connection tests. Please wait a moment before trying again.";
                problemContext.ProblemDetails.Extensions["code"] = ErrorCodes.RateLimitExceeded;

                await problemDetailsService.TryWriteAsync(problemContext);
            };
        });

        return services;
    }
}
