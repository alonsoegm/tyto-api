using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

namespace Tyto.Api.Extensions;

public static class ObservabilityExtensions
{
    private const string ServiceName = "Tyto.Api";

    /// <summary>
    /// Configures structured logging (Serilog) and distributed tracing/metrics (OpenTelemetry).
    /// Serilog is read from the "Serilog" configuration section. OpenTelemetry instruments
    /// ASP.NET Core, outbound HttpClient calls, and the runtime; it exports via OTLP only when an
    /// endpoint is configured (OTEL_EXPORTER_OTLP_ENDPOINT), so local runs stay quiet by default.
    /// </summary>
    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, services, loggerConfiguration) => loggerConfiguration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext());

        var hasOtlpEndpoint = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(
                serviceName: ServiceName,
                serviceVersion: typeof(ObservabilityExtensions).Assembly.GetName().Version?.ToString()))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation(options => options.RecordException = true)
                    .AddHttpClientInstrumentation();

                if (hasOtlpEndpoint)
                    tracing.AddOtlpExporter();
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();

                if (hasOtlpEndpoint)
                    metrics.AddOtlpExporter();
            });

        return builder;
    }
}
