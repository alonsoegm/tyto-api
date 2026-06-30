# Add Metadata API (Dataverse): dynamic Entities & Fields

Status: Implemented
Date: 2026-06-23

## Context

The Configuration flow lets a user pick a target environment and, later, a target entity (table)
and map fields against it. Until now there was no way to retrieve that schema dynamically: the UI
had to rely on static or manual definitions. This risked invalid mappings, produced a poor user
experience, and tightly coupled the UI to assumptions about external systems.

In this codebase a target environment is modeled by the `DatabaseConnection` entity
(`ConnectionType` = `Salesforce` | `MsDataverse`), exposed at `api/database-connections`. Dataverse
ClientSecret authentication is already implemented for the test-connection flow in
`DatabaseConnectionService`, using MSAL and a resilient named HTTP client.

## Problem

- There was no API to retrieve entities (tables) or fields (columns) from an external system.
- Mappings could not be validated against a real external schema.
- The UI was forced to assume schemas instead of discovering them.

## Goal

Introduce a provider-agnostic **Metadata API** that returns a unified shape regardless of source,
following a provider pattern so additional systems (e.g. Salesforce) can be added later without
changing callers. MVP supports **Dataverse only**.

Endpoints (responses are wrapped in the standard `ApiResponse<T>` envelope used across the API):

- `GET /api/metadata/entities?databaseConnectionId={guid}` → `[{ id, name }]`
- `GET /api/metadata/entities/{entityId}/fields?databaseConnectionId={guid}` → `[{ id, name, type }]`

The connection identifier is named `databaseConnectionId` to stay consistent with the existing
`api/database-connections` resource (the original spec called it `targetEnvironmentId`).

## Changes

### 1. Provider-agnostic DTOs

New records in `Tyto.Api/Application/DTOs/Metadata/`:

- `EntityDto(string Id, string Name)`
- `FieldDto(string Id, string Name, string Type)`

### 2. Provider abstraction

`Tyto.Api/Application/Interfaces/IMetadataProvider.cs`:

```csharp
public interface IMetadataProvider
{
    ConnectionType SupportedType { get; }
    Task<Result<List<EntityDto>>> GetEntitiesAsync(DatabaseConnection connection, CancellationToken ct = default);
    Task<Result<List<FieldDto>>> GetFieldsAsync(DatabaseConnection connection, string entityId, CancellationToken ct = default);
}
```

`SupportedType` lets `MetadataService` resolve the right provider from an injected
`IEnumerable<IMetadataProvider>`. Adding Salesforce later is just a new registration.

### 3. Dataverse provider

`Tyto.Api/Application/Services/Metadata/DataverseMetadataProvider.cs`
(`SupportedType => ConnectionType.MsDataverse`):

- Self-contained token acquisition mirroring `DatabaseConnectionService.TestDataverseClientSecretAsync`
  (MSAL `ConfidentialClientApplicationBuilder`, scope `{EnvironmentUrl}/.default`). The working
  test-connection code is left untouched.
- Decrypts the saved `DV_ClientSecret` with the same Data Protection purpose string used to encrypt
  it (`"DatabaseConnection.Dataverse"`).
- MVP supports `ClientSecret` only; `Certificate` / `ManagedIdentity` return a clear failure.
- Calls the Dataverse Web API via the resilient `ExternalHttpClients.ConnectionTest` client at
  `{EnvironmentUrl}/api/data/v9.2`:
  - Entities: `GET /EntityDefinitions?$select=LogicalName,DisplayName`
    → `Id = LogicalName`, `Name =` display label (falls back to `LogicalName`).
  - Fields: `GET /EntityDefinitions(LogicalName='{entityId}')/Attributes?$select=LogicalName,DisplayName,AttributeType`
    → `Id = LogicalName`, `Name =` display label (fallback), `Type = AttributeType`.
- `entityId` is validated against `^[a-zA-Z0-9_]+$` before interpolation (also prevents OData
  single-quote injection).
- Token acquisition is a `protected virtual` seam so tests can exercise the HTTP/mapping logic
  without a real MSAL call.

### 4. Service

`IMetadataService` / `MetadataService` (`Tyto.Api/Application/Services/MetadataService.cs`):

- Loads the `DatabaseConnection` by id (`AsNoTracking`); returns `NotFoundError` if missing.
- Selects the provider whose `SupportedType` matches `connection.ConnectionType`; returns
  `InternalError` if none is registered.
- Delegates to the provider.

### 5. Controller

`Tyto.Api/Controllers/MetadataController.cs` (`[Route("api/metadata")]`): two read endpoints that
map results with the standard `result.ToApiResponse()` / `result.ToErrorResult(this)` helpers.

### 6. Dependency injection

`Tyto.Api/Extensions/ServiceCollectionExtensions.cs`:

```csharp
services.AddScoped<IMetadataService, MetadataService>();
services.AddScoped<IMetadataProvider, DataverseMetadataProvider>();
```

No new HTTP client is needed — the existing `ExternalHttpClients.ConnectionTest` client is reused.

### 7. Tests

- `Tyto.Api.Tests/Services/MetadataServiceTests.cs`: not-found connection, provider selection by
  `ConnectionType`, field delegation, and unsupported-type failure.
- `Tyto.Api.Tests/Services/DataverseMetadataProviderTests.cs`: URL + JSON-to-DTO mapping (incl.
  label fallback) via a stub `HttpMessageHandler`, invalid `entityId` rejected without a network
  call, and non-ClientSecret auth failing cleanly.

This change is purely additive: no entity changes and **no EF migration**.

## Out of scope / future work

- `SalesforceMetadataProvider`.
- Caching, pagination, and advanced filtering.
- Field-type normalization across providers.
- Relationship metadata.
- Certificate / Managed Identity authentication for Dataverse metadata.
