# Tyto API

Admin and configuration REST API for the **Tyto** document intelligence platform. It manages
the building blocks of a document-extraction pipeline: LLM endpoints, Document Intelligence models,
CRM connections (Salesforce / Microsoft Dataverse), extraction configurations, field mappings, and
execution history — with full auditing of every change.

This repository contains a single deployable service (`Tyto.Api`) plus its automated test suite
(`Tyto.Api.Tests`).

---

## Table of Contents

- [Highlights](#highlights)
- [Tech Stack](#tech-stack)
- [Architecture](#architecture)
  - [Layers](#layers)
  - [Request lifecycle](#request-lifecycle)
  - [Result pattern & error handling](#result-pattern--error-handling)
  - [Unit of Work](#unit-of-work)
- [Cross-cutting concerns](#cross-cutting-concerns)
  - [Authentication & authorization](#authentication--authorization)
  - [Rate limiting](#rate-limiting)
  - [CORS](#cors)
  - [Health checks](#health-checks)
  - [Resilience for outbound calls](#resilience-for-outbound-calls)
  - [Observability](#observability)
  - [Secret protection](#secret-protection)
  - [Validation](#validation)
  - [Auditing](#auditing)
- [Project structure](#project-structure)
- [API reference](#api-reference)
- [Configuration reference](#configuration-reference)
- [Local setup](#local-setup)
- [Testing](#testing)
- [Database migrations](#database-migrations)
- [Conventions](#conventions)

---

## Highlights

This service is built to be production-grade, not just functional. It includes:

- **Clean layered architecture** (Domain / Application / Infrastructure / Controllers) with thin controllers.
- **Railway-oriented error handling** via [FluentResults](https://github.com/altmann/FluentResults) — no exceptions for control flow — mapped to RFC 7807 `ProblemDetails`.
- **Unit of Work** middleware that commits the database transaction once per request, atomically.
- **Config-driven authentication toggle** + a global fallback authorization policy (no per-controller `[Authorize]` needed).
- **Rate limiting** on expensive outbound "test connection" endpoints.
- **Resilient outbound HTTP** (retry / timeout / circuit breaker) via `IHttpClientFactory` + Polly.
- **First-class observability**: structured logging (Serilog), distributed tracing + metrics (OpenTelemetry), and an end-to-end correlation id.
- **Liveness & readiness** health checks for orchestrators.
- **Secrets encrypted at rest** and never returned in any response.
- **Automated tests** (unit + HTTP-level integration) covering services, security rules, and the full pipeline.

---

## Tech Stack

| Concern | Technology |
|---|---|
| Runtime | .NET 10 / ASP.NET Core |
| Database | PostgreSQL (Npgsql + EF Core 10, Code First) |
| Authentication | Azure AD — `Microsoft.Identity.Web` (JWT Bearer) |
| Validation | FluentValidation |
| Mapping | Mapster |
| Result/error model | FluentResults + `ProblemDetails` (RFC 7807) |
| Secret encryption | ASP.NET Core Data Protection |
| Resilience | `Microsoft.Extensions.Http.Resilience` (Polly) |
| Logging | Serilog |
| Tracing & metrics | OpenTelemetry (OTLP exporter) |
| API docs | Swagger / OpenAPI (OAuth2 + PKCE) |
| Testing | xUnit, Moq, FluentAssertions, EF Core InMemory, `WebApplicationFactory` |

---

## Architecture

### Layers

The project follows a layered (clean-architecture-inspired) structure inside a single project:

```
Controllers  ──►  Application (Services)  ──►  Domain (Entities/Enums)
     │                    │
     │                    └──►  Infrastructure (DbContext, EF configs, mapping, middleware)
     └─ thin HTTP layer: bind request → call service → translate Result → HTTP response
```

- **Domain** — entities and enums. No dependencies on any other layer.
- **Application** — services (business logic), DTOs, interfaces, validators, and common building
  blocks (`ApiResponse`, `PagedResult`, error types, constants). Returns `Result`/`Result<T>`.
- **Infrastructure** — `TytoDbContext`, EF entity configurations, Mapster config, middleware,
  and migrations.
- **Controllers** — thin. They bind input, call a service, and translate the `Result` into either a
  success envelope or a `ProblemDetails` error. No business logic, DB access, or mapping.

### Request lifecycle

```
HTTP request
  │
  ▼
Exception handler (RFC 7807 ProblemDetails)
  ▼
Correlation ID  (X-Correlation-ID → log context + response header)
  ▼
Serilog request logging  (method, path, status, duration)
  ▼
Unit of Work  (defers commit to end of request)
  ▼
HTTPS redirect → CORS → Rate limiter → Authentication → Authorization
  ▼
Controller → Service → DbContext (changes tracked, not yet saved)
  ▼
Unit of Work commits once if the response is 2xx and there are pending changes
```

### Result pattern & error handling

Services never throw for expected failures and never return `null`. They return `Result<T>` carrying
a typed error (`NotFoundError`, `ConflictError`, `ValidationError`, `InternalError`). Controllers call
`result.ToErrorResult(this)`, which maps the error to the correct status code and a `ProblemDetails`
body with a stable machine-readable `code`:

| Error type | HTTP status | `code` |
|---|---|---|
| `ValidationError` | 400 | `VALIDATION_ERROR` |
| `NotFoundError` | 404 | `NOT_FOUND` |
| `ConflictError` | 409 | `CONFLICT` |
| Rate limit exceeded | 429 | `RATE_LIMIT_EXCEEDED` |
| `InternalError` / unhandled | 500 | `INTERNAL_ERROR` |

A `GlobalExceptionHandler` (registered via `IExceptionHandler`) is the safety net for anything
unhandled — it logs the exception and returns a sanitized `ProblemDetails` (stack traces are never
exposed). Successful responses use a unified envelope:

```json
{ "success": true,  "data": { ... }, "error": null }
```

### Unit of Work

`UnitOfWorkMiddleware` defers `SaveChangesAsync` until the end of the request. Services add/modify/
remove entities on the tracked `DbContext` but do **not** save. After the controller runs, the
middleware commits **once** if the response is `2xx` and there are pending changes; otherwise changes
are discarded with the `DbContext`. This makes each request an atomic transaction and keeps audit
writes in the same transaction as the change that produced them.

---

## Cross-cutting concerns

### Authentication & authorization

- Azure AD JWT Bearer is always registered (tokens are validated whenever present).
- A **global fallback authorization policy** governs access, controlled by `Authentication:Enabled`:
  - `true` → every endpoint requires an authenticated user.
  - `false` → endpoints are reachable anonymously (intended for local development).
- This removes the need for per-controller `[Authorize]` attributes and lets you toggle auth purely
  through configuration/environment, without code changes. Health endpoints are always anonymous.

### Rate limiting

The outbound **"test connection"** endpoints (`POST /api/{resource}/test-connection`) trigger costly
external calls (Azure OpenAI/Foundry, Document Intelligence, CRM databases) and are a natural abuse
vector. They are protected by a fixed-window policy (5 requests/minute), partitioned by authenticated
user (object id) or client IP. Rejections return `429` with a `ProblemDetails` body and
`code: RATE_LIMIT_EXCEEDED`. Other endpoints are not rate limited.

### CORS

Origins are read from `Cors:AllowedOrigins`. When configured, only those origins are allowed. When no
origins are configured, CORS is permissive **only in the Development environment**; in any other
environment it stays closed (the app still starts, but cross-origin requests are blocked until origins
are set).

### Health checks

| Endpoint | Purpose | Checks |
|---|---|---|
| `GET /health/live` | Liveness — is the process up? | none (always `200` if responding) |
| `GET /health/ready` | Readiness — can it serve traffic? | PostgreSQL connectivity |

Both are anonymous so orchestrators (Kubernetes, Azure Container Apps) can probe them without a token.

### Resilience for outbound calls

All external connection tests go through a named `IHttpClientFactory` client
(`ExternalHttpClients.ConnectionTest`) configured with the **standard resilience pipeline**
(`AddStandardResilienceHandler`): retries on transient errors, per-attempt and total timeouts, and a
circuit breaker. This eliminates socket-exhaustion risk from ad-hoc `new HttpClient()` usage and makes
flaky upstreams degrade gracefully.

### Observability

- **Structured logging** — Serilog, configured from the `Serilog` config section. Per-request logs
  (method, path, status, duration) via `UseSerilogRequestLogging`.
- **Correlation ID** — every request gets an id from the client `X-Correlation-ID` header, falling
  back to the current trace id, then a new GUID. It is echoed on the response and attached to every
  log line for the request.
- **Distributed tracing & metrics** — OpenTelemetry instruments ASP.NET Core, outbound `HttpClient`
  calls, and the .NET runtime. Telemetry is exported via **OTLP only when
  `OTEL_EXPORTER_OTLP_ENDPOINT` is set**, so local runs stay quiet by default.

### Secret protection

Sensitive fields (`ApiKey`, `SF_ClientSecret`, `SF_PrivateKeyFile`, `SF_Passphrase`,
`DV_ClientSecret`, `DV_CertificateData`) are **write-only**: accepted on create/update, encrypted at
rest with the Data Protection API, and **never** mapped to any response DTO or written to logs. A unit
test enforces this by reflecting over every `*ResponseDto` to fail the build if a secret field is ever
exposed.

### Validation

Each create/update DTO has a `FluentValidation` `AbstractValidator<T>`, auto-registered from the
assembly. Services validate first and return a `ValidationError` (mapped to a `400` with per-field
errors) before touching the database.

### Auditing

Every create, update, and delete is recorded in an `AuditLog` (action, entity type/id, who, when).
Because of the Unit of Work, audit entries are committed in the same transaction as the change.

---

## Project structure

```
Tyto.Api/
├── Application/
│   ├── Common/            # ApiResponse, PagedResult, errors, constants, validation, utils
│   ├── DTOs/              # Request/response records per resource
│   ├── Interfaces/        # Service contracts (+ IUnitOfWork)
│   └── Services/          # Business logic (returns Result/Result<T>)
├── Controllers/           # Thin HTTP layer
├── Domain/
│   ├── Entities/          # BaseEntity + domain entities
│   └── Enums/             # Stored as strings in the DB
├── Extensions/            # AddDatabase, AddAzureAdAuth, AddCorsPolicy, AddHealthChecksConfig,
│                          # AddRateLimitingConfig, AddObservability, AddApplicationServices, Swagger
├── Infrastructure/
│   ├── Data/              # TytoDbContext, UnitOfWork, EF entity configurations
│   ├── ExceptionHandlers/ # GlobalExceptionHandler (IExceptionHandler)
│   ├── Mapping/           # Mapster configuration (ignores secret fields)
│   ├── Middleware/        # UnitOfWork, CorrelationId
│   └── Migrations/        # EF Core migrations
├── Validators/            # FluentValidation validators per DTO
├── Program.cs             # Thin bootstrap — all wiring lives in Extensions/
└── appsettings*.json

Tyto.Api.Tests/
├── Infrastructure/        # InMemory DbContext factory, Mapster module initializer
├── Services/              # Unit tests for services
├── Mapping/               # Sensitive-field exposure tests
└── Integration/           # WebApplicationFactory tests (auth, rate limit, health, error format)
```

---

## API reference

All routes are under `/api`. Resource bodies are JSON; enums are serialized as strings.

| Resource | Base route | Operations |
|---|---|---|
| Language Models | `/api/language-models` | List, Get, Create, Update, Delete, **Test connection** |
| Document Models | `/api/document-models` | List, Get, Create, Update, Delete, **Test connection** |
| Database Connections | `/api/database-connections` | List, Get, Create, Update, Delete, **Test connection** |
| Configurations | `/api/configurations` | List, Get, Create, Update, Delete |
| Mapped Fields | `/api/mapped-fields` | List (by `configurationId`), Get, Create, Update, Delete |
| Run History | `/api/run-history` | Read-only (List, Get) |
| Audit Logs | `/api/audit-logs` | Read-only (List, Get) |

**Supported providers**

- Language / Document models: `AzureOpenAI`, `AzureFoundry`, `OpenAI`.
- Database connections: `Salesforce`, `MsDataverse` (multiple auth methods each, including Key Vault–backed secrets).

**List queries** accept `page`, `pageSize` (capped), `search`, `sortBy`, `sortDescending` and return a
paged envelope (`items`, `totalCount`, `page`, `pageSize`, `totalPages`, `hasPreviousPage`,
`hasNextPage`).

Interactive documentation (with Azure AD login) is available at `/swagger` in Development.

---

## Configuration reference

| Key | Description |
|---|---|
| `Authentication:Enabled` | `true` requires auth on all endpoints; `false` allows anonymous (dev). |
| `AzureAd:Instance` | Azure AD instance (`https://login.microsoftonline.com/`). |
| `AzureAd:TenantId` | Azure AD tenant identifier. |
| `AzureAd:ClientId` | App registration client id. |
| `AzureAd:Audience` | Token audience (`api://<client-id>`). |
| `ConnectionStrings:TytoDb` | PostgreSQL connection string. |
| `Cors:AllowedOrigins` | Array of allowed origins (empty → permissive in Development only). |
| `Serilog` | Serilog configuration (minimum levels, sinks). |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | OTLP collector endpoint; when set, traces/metrics are exported. |

> Never commit real credentials. Use **.NET User Secrets** locally and a secret store (e.g. Azure Key
> Vault) / environment variables in deployed environments.

---

## Local setup

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [PostgreSQL](https://www.postgresql.org/download/)
- An [Azure AD App Registration](https://portal.azure.com/) (only needed when running with auth enabled):
  - Exposed API scope: `access_as_user`
  - SPA redirect URI for Swagger: `https://localhost:7170/swagger/oauth2-redirect.html`

### Steps

```bash
# 1. Create the local database
psql -U postgres -c "CREATE DATABASE tyto_dev;"

# 2. Configure secrets (from Tyto.Api/)
cd Tyto.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:TytoDb" "Host=localhost;Port=5432;Database=tyto_dev;Username=<pg-user>"
# Only if running with auth enabled:
dotnet user-secrets set "AzureAd:TenantId" "<tenant-id>"
dotnet user-secrets set "AzureAd:ClientId" "<client-id>"
dotnet user-secrets set "AzureAd:Audience" "api://<client-id>"

# 3. Apply migrations
dotnet ef database update

# 4. Run
dotnet run --launch-profile https
```

By default the `Development` environment has `Authentication:Enabled = false`, so you can call the API
without a token. Swagger UI: `https://localhost:7170/swagger`.

---

## Testing

```bash
# From the repository root
dotnet test
```

The test suite has two tiers:

- **Unit tests** — services tested against an EF Core InMemory `DbContext` with mocked collaborators;
  plus a reflection-based test that fails if any response DTO exposes a secret field.
- **Integration tests** — the real pipeline booted with `WebApplicationFactory<Program>` (database
  swapped for InMemory), verifying the auth toggle (`200` vs `401`), rate limiting (`429` +
  `RATE_LIMIT_EXCEEDED`), the liveness probe, and the success/`ProblemDetails` response shapes.

> A future tier of integration tests against a real PostgreSQL container (Testcontainers) requires a
> running Docker daemon.

---

## Database migrations

```bash
cd Tyto.Api
dotnet ef migrations add <MigrationName> --output-dir Infrastructure/Migrations
dotnet ef database update
```

EF Core conventions:

- Enums are stored as strings.
- `BaseEntity` timestamps (`CreatedAt`/`UpdatedAt`) are maintained centrally.
- Entity configurations live in `Infrastructure/Data/Configurations` and are applied via
  `ApplyConfigurationsFromAssembly`.

---

## Conventions

Engineering standards (response envelope, thin controllers, validation, logging, security rules,
mapping, async/cancellation, naming) are documented in
[`.github/copilot-instructions.md`](.github/copilot-instructions.md). All code, comments, and
documentation are written in **English**.
