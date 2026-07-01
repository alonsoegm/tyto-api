using System.Text.Json.Serialization;
using Serilog;
using Tyto.Api.Extensions;
using Tyto.Api.Infrastructure.ExceptionHandlers;

var builder = WebApplication.CreateBuilder(args);

builder.AddObservability();
builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddAzureAdAuth(builder.Configuration);
builder.Services.AddApplicationServices();
builder.Services.AddSwaggerWithOAuth(builder.Configuration);
builder.Services.AddCorsPolicy(builder.Configuration, builder.Environment);
builder.Services.AddHealthChecksConfig(builder.Configuration);
builder.Services.AddRateLimitingConfig();
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = ctx =>
        ctx.ProblemDetails.Instance = $"{ctx.HttpContext.Request.Method} {ctx.HttpContext.Request.Path}");
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseCorrelationId();
app.UseSerilogRequestLogging();
app.UseUnitOfWork();

if (app.Environment.IsDevelopment())
    app.UseSwaggerWithOAuth(app.Configuration);

app.UseHttpsRedirection();
app.UseCorsPolicy();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthCheckEndpoints();

app.Run();

// Exposed so the integration test project (WebApplicationFactory&lt;Program&gt;) can reference the entry point.
public partial class Program { }

