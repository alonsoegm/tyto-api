# Tyto API — Copilot Instructions

This file defines the standards, conventions, and architecture decisions for the Tyto API.
Always follow these rules when generating, modifying, or reviewing code in this repository.

---

## Project Overview

**Tyto Admin API** manages all configuration and pipeline definitions for the Tyto
document intelligence platform: LLM endpoints, Document Intelligence models, CRM connections
(Salesforce / Microsoft Dataverse), extraction configurations, field mappings, and execution history.

- **Runtime**: .NET 10 / ASP.NET Core
- **Database**: Azure SQL Database / SQL Server via EF Core 10 SqlServer provider (Code First)
- **Auth**: Azure AD (Microsoft.Identity.Web — JWT Bearer) behind a config-driven global policy
- **Architecture**: Single project (`Tyto.Api`) with a layered folder structure, plus a test project (`Tyto.Api.Tests`)
- **Error model**: Result pattern (FluentResults) → RFC 7807 `ProblemDetails`; no exceptions for control flow
- **Transactions**: Unit of Work — one commit per request via middleware

---

## Folder Structure

```
Tyto.Api/
├── Application/
│   ├── Common/
│   │   ├── ApiResponse.cs            # Unified success envelope
│   │   ├── ApiError.cs
│   │   ├── PagedResult.cs
│   │   ├── QueryParameters.cs
│   │   ├── ResultExtensions.cs       # Result -> ApiResponse / ProblemDetails (ToApiResponse, ToErrorResult)
│   │   ├── Constants/
│   │   │   ├── AppClaimTypes.cs       # Azure AD claim name constants
│   │   │   ├── ErrorCodes.cs          # Error code string constants
│   │   │   ├── ExternalHttpClients.cs # Named IHttpClientFactory client names
│   │   │   └── PaginationDefaults.cs
│   │   ├── Errors/                    # FluentResults IError types
│   │   │   ├── NotFoundError.cs
│   │   │   ├── ConflictError.cs
│   │   │   ├── ValidationError.cs
│   │   │   └── InternalError.cs
│   │   ├── Validation/
│   │   │   └── ValidationExtensions.cs  # ValidateToResultAsync
│   │   └── Utils/
│   ├── DTOs/                          # Request/response records per resource
│   ├── Interfaces/                    # Service contracts + IUnitOfWork
│   └── Services/                      # Business logic (returns Result/Result<T>)
├── Controllers/                       # Thin HTTP layer
├── Domain/
│   ├── Entities/                      # BaseEntity + domain entities
│   └── Enums/                         # Stored as strings
├── Extensions/
│   ├── DatabaseExtensions.cs          # AddDatabase()
│   ├── AuthExtensions.cs              # AddAzureAdAuth() — registers global fallback policy
│   ├── ServiceCollectionExtensions.cs # AddApplicationServices()
│   ├── SwaggerExtensions.cs           # AddSwaggerWithOAuth() / UseSwaggerWithOAuth()
│   ├── CorsExtensions.cs              # AddCorsPolicy(config, env) / UseCorsPolicy()
│   ├── HealthCheckExtensions.cs       # AddHealthChecksConfig() / MapHealthCheckEndpoints()
│   ├── RateLimitingExtensions.cs      # AddRateLimitingConfig() + TestConnectionPolicy
│   ├── ObservabilityExtensions.cs     # AddObservability() — Serilog + OpenTelemetry
│   └── MiddlewareExtensions.cs        # UseUnitOfWork() / UseCorrelationId()
├── Infrastructure/
│   ├── Data/
│   │   ├── TytoDbContext.cs
│   │   ├── UnitOfWork.cs
│   │   └── Configurations/            # IEntityTypeConfiguration<T> per entity
│   ├── ExceptionHandlers/
│   │   └── GlobalExceptionHandler.cs  # IExceptionHandler — safety net for unhandled exceptions
│   ├── Mapping/
│   │   └── MappingConfig.cs           # Mapster configuration (ignores secret fields)
│   ├── Middleware/
│   │   ├── UnitOfWorkMiddleware.cs
│   │   └── CorrelationIdMiddleware.cs
│   └── Migrations/
├── Validators/                        # FluentValidation AbstractValidator<T> per DTO
├── Program.cs                         # Thin bootstrap
├── appsettings.json
└── appsettings.Development.json

Tyto.Api.Tests/
├── Infrastructure/                    # TestDbContextFactory (InMemory), Mapster module initializer
├── Services/                          # Unit tests for services
├── Mapping/                           # Sensitive-field exposure tests
└── Integration/                       # WebApplicationFactory tests
```

---

## Response & Error Model

Success responses use `ApiResponse<T>`. Failures use RFC 7807 `ProblemDetails` (not `ApiResponse`).

```json
// Success — ApiResponse<T>
{ "success": true, "data": { ... }, "error": null }

// Failure — ProblemDetails with a machine-readable "code"
{ "type": "...", "title": "...", "status": 404, "detail": "Language model ... was not found.",
  "instance": "GET /api/language-models/...", "code": "NOT_FOUND" }
```

- Build success with `result.ToApiResponse()` and return `Ok(...)` / `CreatedAtAction(...)` / `NoContent()`.
- Build failure with `result.ToErrorResult(this)` — it maps the error type to the right status code and `ProblemDetails`.
- Error codes are constants in `ErrorCodes.cs`. Never use magic strings.

---

## Result Pattern & Error Types

Services return `Result` / `Result<T>` (FluentResults). They **never throw for expected failures and
never return null** for missing entities. Use the typed errors in `Application/Common/Errors/`:

| Error type | HTTP status | `code` |
|---|---|---|
| `ValidationError` | 400 | `VALIDATION_ERROR` |
| `NotFoundError` | 404 | `NOT_FOUND` |
| `ConflictError` | 409 | `CONFLICT` |
| (rate limit) | 429 | `RATE_LIMIT_EXCEEDED` |
| `InternalError` / unhandled | 500 | `INTERNAL_ERROR` |

```csharp
// Correct — service
if (entity is null)
    return Result.Fail<LanguageModelResponseDto>(new NotFoundError(nameof(LanguageModel), id));
```

`ToErrorResult` / `ToApiResponse` and the status-code mapping live in `Application/Common/ResultExtensions.cs`.

---

## Program.cs Convention

`Program.cs` stays thin. All DI registration and pipeline configuration live in extension methods
under `Extensions/`. Each extension handles one concern and receives `IConfiguration`/`IWebHostEnvironment`
when it needs them.

```csharp
// Service registration
builder.AddObservability();                                   // Serilog + OpenTelemetry
builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddAzureAdAuth(builder.Configuration);
builder.Services.AddApplicationServices();
builder.Services.AddSwaggerWithOAuth(builder.Configuration);
builder.Services.AddCorsPolicy(builder.Configuration, builder.Environment);
builder.Services.AddHealthChecksConfig(builder.Configuration);
builder.Services.AddRateLimitingConfig();
builder.Services.AddControllers().AddJsonOptions(/* JsonStringEnumConverter */);
builder.Services.AddProblemDetails(...);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Pipeline (order matters)
app.UseExceptionHandler();
app.UseCorrelationId();
app.UseSerilogRequestLogging();
app.UseUnitOfWork();
if (app.Environment.IsDevelopment()) app.UseSwaggerWithOAuth(app.Configuration);
app.UseHttpsRedirection();
app.UseCorsPolicy();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthCheckEndpoints();
```

---

## Controllers

- Controllers are **thin**: bind request → call service → translate `Result` → response. No business
  logic, no direct DB access, no mapping.
- Do **not** put `[Authorize]` on controllers. Authorization is enforced globally by a fallback policy
  (see Authentication & Authorization). Use `[AllowAnonymous]` only for deliberate exceptions.
- Apply `[EnableRateLimiting(RateLimitingExtensions.TestConnectionPolicy)]` to `test-connection` actions.
- Return correct status codes: 200, 201 (`CreatedAtAction`), 204 (`NoContent`), plus 400/404/409/429/500 via `ToErrorResult`.

```csharp
[HttpGet("{id:guid}")]
public async Task<ActionResult<ApiResponse<LanguageModelResponseDto>>> GetById(
    Guid id, CancellationToken cancellationToken)
{
    var result = await _service.GetByIdAsync(id, cancellationToken);
    if (result.IsFailed)
        return result.ToErrorResult(this);
    return Ok(result.ToApiResponse());
}
```

The acting user is resolved from claims (`AppClaimTypes.ObjectId` → `Email` → `"unknown"`).

---

## Services

- Services implement their interface and contain all business logic.
- All methods are `async` and accept `CancellationToken cancellationToken = default` (except the
  synchronous `IAuditLogService.Log(...)`).
- Validate input first with `await _validator.ValidateToResultAsync(dto, cancellationToken)`; on failure
  return `Result.Fail<T>(validation.Errors)`.
- Return typed errors (`NotFoundError`, `ConflictError`, …) — never throw for expected failures, never return null.
- **Do not call `SaveChangesAsync` yourself.** Add/modify/remove tracked entities; the Unit of Work
  middleware commits once at the end of the request. (Exception: a service may save when it must read
  back a generated value mid-request, but the default is to defer.)
- Call `IAuditLogService.Log(...)` on every Create, Update, and Delete.
- Services that handle secrets (`LanguageModelService`, `DocumentModelService`, `DatabaseConnectionService`)
  encrypt them with `IDataProtector` (ASP.NET Data Protection).
- Outbound HTTP must use `IHttpClientFactory.CreateClient(ExternalHttpClients.ConnectionTest)` — never `new HttpClient()`.

---

## Unit of Work

- `UnitOfWorkMiddleware` defers the commit to the end of the request.
- After the controller runs, it calls `SaveChangesAsync` **once** if the response is `2xx` and there
  are pending changes (`IUnitOfWork.HasChanges()`); otherwise changes are discarded.
- This makes each request an atomic transaction and keeps audit writes in the same transaction as the
  change that produced them.

---

## Validation

- One `AbstractValidator<T>` per Create/Update DTO, in `Validators/{Entity}/`.
- Registered automatically via `AddValidatorsFromAssemblyContaining<Program>()`.
- Invoked in services with `ValidateToResultAsync` (returns a `Result`, does **not** throw). Failures
  surface as `ValidationError` → `400` with per-field errors.

---

## Error Handling

- Expected failures flow through the Result pattern and are mapped to status codes by
  `ResultExtensions.ToErrorResult` (by **error type**, not by exception type).
- `GlobalExceptionHandler` (registered via `AddExceptionHandler<>` + `app.UseExceptionHandler()`) is the
  safety net for **unhandled** exceptions: it logs them and returns a sanitized `ProblemDetails`.
- Stack traces are **never** exposed in responses. Error codes are constants in `ErrorCodes.cs`.

---

## Authentication & Authorization

- Azure AD JWT Bearer is always registered (`AddMicrosoftIdentityWebApiAuthentication`); tokens are
  validated whenever present.
- Access is governed by a **global fallback authorization policy**, toggled by `Authentication:Enabled`:
  - `true` → every endpoint requires an authenticated user.
  - `false` → endpoints are reachable anonymously (local development only).
- This is why controllers carry no `[Authorize]`. Health endpoints are always anonymous.

---

## Rate Limiting

- The `test-connection` endpoints trigger costly outbound calls and are protected by a fixed-window
  policy (`RateLimitingExtensions.TestConnectionPolicy`, 5 req/min), partitioned by user object id or
  client IP.
- Rejections return `429` with a `ProblemDetails` body and `code: RATE_LIMIT_EXCEEDED`.
- Apply via `[EnableRateLimiting(RateLimitingExtensions.TestConnectionPolicy)]` on the action only.

---

## CORS

- Origins come from `Cors:AllowedOrigins`. When set, only those are allowed.
- With no origins configured, CORS is permissive **only in Development**; in any other environment it
  stays closed (app still starts, cross-origin blocked until configured).

---

## Health Checks

- `GET /health/live` — liveness (no checks; `200` while the process responds).
- `GET /health/ready` — readiness (SQL Server connectivity, tagged `ready`).
- Both anonymous, mapped in `MapHealthCheckEndpoints()`.

---

## Resilient Outbound HTTP

- All external connection tests use the named client `ExternalHttpClients.ConnectionTest`, configured
  with `AddStandardResilienceHandler()` (retry on transient errors, per-attempt/total timeouts, circuit breaker).
- Cache `TokenCredential` (`DefaultAzureCredential`) as a field; do not construct it per call.

---

## Observability

- **Logging**: Serilog, configured from the `Serilog` config section (`AddObservability` → `UseSerilog`).
  Per-request logs via `app.UseSerilogRequestLogging()`. Use `ILogger<T>` via constructor — no static loggers.
- **Correlation ID**: `CorrelationIdMiddleware` sets `X-Correlation-ID` (from the client header, else
  trace id, else a GUID), echoes it on the response, and pushes it into the Serilog log context.
- **Tracing & metrics**: OpenTelemetry instruments ASP.NET Core, `HttpClient`, and the runtime; exported
  via OTLP only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set.
- Log `Information` on Create/Delete/TestConnection, `Warning` on NotFound/Conflict/validation failures,
  `Error` on unexpected exceptions. **Never log** any sensitive field (see Security Rules).

---

## Mapping (Mapster)

- Centralized in `Infrastructure/Mapping/MappingConfig.cs`; registered in `AddApplicationServices`.
- Simple 1:1 mappings use Mapster convention.
- Sensitive fields must be explicitly **ignored** in mappings toward entities/response DTOs.
- Never map encrypted/secret fields to any outbound DTO.

---

## Security Rules

These fields are **write-only** — accepted on create/update, stored encrypted, and must **never** appear
in any response DTO or log output:

- `ApiKeyEncrypted` (and the inbound `ApiKey`)
- `SF_ClientSecret`, `SF_PrivateKeyFile`, `SF_Passphrase`
- `DV_ClientSecret`, `DV_CertificateData`

A unit test (`SensitiveFieldMappingTests`) reflects over every `*ResponseDto` and fails the build if a
secret field is ever exposed. Keep it passing.

---

## XML Documentation

Apply `/// <summary>` to:
- All interface methods
- All public service methods (use `<inheritdoc />` if the interface documents it)
- All controller actions (`<summary>`, `<param>`, `<returns>`)
- All validator classes

Do **not** document: constructors, auto-properties, DTOs, entity classes, enums.
`<GenerateDocumentationFile>true</GenerateDocumentationFile>` is enabled and Swagger includes the XML.

---

## Constants & Utils

Never use magic strings. Reference constants in `Application/Common/Constants/`
(`ErrorCodes`, `AppClaimTypes`, `PaginationDefaults`, `ExternalHttpClients`). Utils live in
`Application/Common/Utils/`.

---

## EF Core Conventions

- `TytoDbContext` uses `ApplyConfigurationsFromAssembly` — all entity config via `IEntityTypeConfiguration<T>`.
- All enums stored as strings: `HasConversion<string>().HasMaxLength(...)`.
- `SaveChangesAsync` override sets `UpdatedAt = DateTime.UtcNow` on modified `BaseEntity` entries;
  `Id`/`CreatedAt` are initialized on the entity.
- FK delete behaviors: Cascade on `Configuration → MappedFields/RunHistory`, Restrict on
  `→ LanguageModel/DocumentModel/DatabaseConnection`, SetNull on optional `DocumentModelId`.
- `AuditLog` has no FK constraints (polymorphic by `EntityType` + `EntityId`).
- Migrations output dir: `Infrastructure/Migrations/`.

---

## Async & CancellationToken

- Service methods and controller actions are `async Task<T>` and include `CancellationToken cancellationToken = default`
  (except synchronous helpers like `IAuditLogService.Log`).
- Never use `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()`.

---

## Testing

- Test project: `Tyto.Api.Tests` (xUnit). Run with `dotnet test`.
- **Unit tests** use an EF Core InMemory `TytoDbContext` (`TestDbContextFactory`) with real
  validators and mocked collaborators (Moq). Because services defer the commit, call
  `SaveChangesAsync` in the test to simulate the Unit of Work when asserting persistence.
- **Integration tests** boot the real pipeline with `WebApplicationFactory<Program>` (DB swapped for
  InMemory). Use `AuthEnabledWebApplicationFactory` to exercise the auth-on path. Override config that
  `Program` reads at startup via `UseSetting`, not `ConfigureAppConfiguration`.
- Keep the sensitive-field exposure test green.
- Assertions use FluentAssertions.

---

## NuGet Packages

| Package | Purpose |
|---|---|
| `Microsoft.EntityFrameworkCore.SqlServer` | EF Core provider for Azure SQL / SQL Server |
| `Microsoft.Identity.Web` | Azure AD JWT authentication |
| `Swashbuckle.AspNetCore` | Swagger UI |
| `FluentValidation.AspNetCore` | DTO validation |
| `FluentResults` | Result/error model |
| `Mapster` | Entity ↔ DTO mapping |
| `Microsoft.AspNetCore.DataProtection` | Secret encryption at rest |
| `AspNetCore.HealthChecks.SqlServer` | SQL Server health check |
| `Microsoft.Extensions.Http.Resilience` | Polly resilience for outbound HTTP |
| `Serilog.AspNetCore` | Structured logging |
| `OpenTelemetry.Extensions.Hosting` + `Instrumentation.AspNetCore` / `.Http` / `.Runtime` + `Exporter.OpenTelemetryProtocol` | Tracing & metrics |
| `Azure.AI.OpenAI`, `Azure.AI.Inference`, `Azure.Identity` | Provider SDKs for connection tests |

Test project (`Tyto.Api.Tests`):
- `xunit`, `Moq`, `FluentAssertions`, `Microsoft.EntityFrameworkCore.InMemory`, `Microsoft.AspNetCore.Mvc.Testing`

---

## Language

All code, variable names, constants, XML documentation, comments, error messages,
and commit messages must be written in **English**.
