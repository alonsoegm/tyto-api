# Make Document Model `ModelId` Optional + Default to `prebuilt-layout`

Status: Implemented
Date: 2026-06-23

## Context

The current implementation models Azure Document Intelligence **models** as independent
entities that each carry their own infrastructure (endpoint, authentication, API version).
This misrepresents Azure's architecture, where a single **resource** exposes all models and
a model is selected dynamically via `modelId` at request time.

Because the system treated `ModelId` as a required field, it over-constrained valid
scenarios (Azure does not require pre-registration of models) and blocked the planned
"default + override" configuration work.

## Problem

- The system required a `ModelId` to exist on every `DocumentModel`.
- Validation forced `ModelId` to be non-empty across DTOs, the domain entity, and the
  database column.
- Azure Document Intelligence does not require this: a model can be chosen at runtime.

## Goal (minimal, temporary simplification)

This change does **not** perform the full resource/model split. It only:

1. Makes `ModelId` optional everywhere (no required validation; null/empty allowed).
2. Always falls back to a default model when one is needed.
3. Uses `"prebuilt-layout"` as the default for the Test Connection flow.

## Changes

### 1. `ModelId` is optional end-to-end

- Domain entity `DocumentModel.ModelId`: `string` → `string?`
  (`Tyto.Api/Domain/Entities/DocumentModel.cs`).
- DTOs `ModelId`: `string` → `string?`
  (`DocumentModelCreateDto`, `DocumentModelUpdateDto`, `DocumentModelResponseDto`).
  `TestDocumentModelConnectionDto.ModelId` was already optional.
- Validators: removed `NotEmpty()` from `ModelId` (kept `MaximumLength(200)`) in
  `DocumentModelCreateValidator` and `DocumentModelUpdateValidator`.
- EF configuration: removed `IsRequired()` from `ModelId` in
  `DocumentModelConfiguration` (kept `HasMaxLength(200)`).
- Migration `MakeDocumentModelModelIdNullable`: drops the `NOT NULL` constraint on the
  `DocumentModels.ModelId` column.

### 2. Test Connection default

In `DocumentModelService`:

- Added `private const string DefaultModelId = "prebuilt-layout";`.
- In `TestConnectionAsync`, when `dto.ModelId` is null/empty the request now targets
  `prebuilt-layout`:

  ```csharp
  var modelId = string.IsNullOrWhiteSpace(dto.ModelId) ? DefaultModelId : dto.ModelId.Trim();
  var requestUrl = BuildAzureRequestUrl(endpoint, modelId, apiVersion);
  ```

- This also covers the create flow, since `CreateAsync` runs the connection test through
  `TestConnectionAsync`.

### Persistence behavior

The default is applied **only when a model is needed** (at test/runtime), not on save. A
`DocumentModel` created/updated without a `ModelId` is stored as `NULL`; we do not persist
`prebuilt-layout`.

## Out of scope / future work

- Splitting infrastructure (endpoint/auth/API version) into a
  `DocumentIntelligenceResource` entity separate from model selection.
- Removing endpoint/auth duplication across models.
- Renaming the `DocumentModel` entity/controller or changing routes.
