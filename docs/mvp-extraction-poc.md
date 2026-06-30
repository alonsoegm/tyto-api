# MVP / POC — Execute extraction from a saved Configuration

> **Status:** proposed plan (not yet implemented).
> **Related:** [legacy-document-processing.md](./legacy-document-processing.md) — the legacy
> engine this MVP ports its core logic from.

## Context

The new Tyto API already models every building block of an extraction pipeline
(`Configuration`, `LanguageModel`, `DocumentModel`, `DatabaseConnection`, `MappedField`,
`RunHistory`) and exposes full CRUD, but there is **no execution engine** — nothing that
takes a document and a saved configuration and actually runs the extraction. The legacy
Tyto had that engine (parse → optionally Document Intelligence → LLM → structured JSON).

This POC builds the **smallest end-to-end slice** that proves two things:

1. A saved `Configuration` actually carries everything needed to run an extraction.
2. We can see the real shape of the execution process before investing in decoupling/scaling.

**Scope (agreed):**

- The run **only returns the extracted JSON** (shown in the UI) and **persists a `RunHistory`**.
  It does **NOT** write to the destination Salesforce/Dataverse connection yet.
- The text-acquisition path is **driven by the Configuration**: if it has a `DocumentModel`,
  use Azure Document Intelligence; otherwise extract text locally. The LLM
  (`LanguageModel`) always normalizes the result into the schema built from `MappedField`s.
- UI goal: **select a configuration → drop a document → run → see the result.**

This is intentionally a coupled, synchronous vertical slice. Decoupling (queue/batch,
destination writes, classifier/multi-section) comes later.

---

## Backend — `Tyto.Api`

### New endpoint

Add to `Controllers/ConfigurationsController.cs`:

- `POST api/configurations/{id:guid}/extract` — accepts `[FromForm] IFormFile file`,
  returns `ApiResponse<ExtractionResultDto>`. Validates the upload against the
  configuration's `AcceptedFileTypes` and `MaxUploadSizeMB` (already on the entity).
  Follows the existing `result.ToErrorResult(this)` / `result.ToApiResponse()` pattern.

### New service + helpers (`Application/Services/Extraction/`)

- `IExtractionService` (in `Application/Interfaces/`) + `ExtractionService` — orchestrator:
  1. Load the `Configuration` with `LanguageModel`, `DocumentModel`, and `MappedFields`
     (include the self-referencing field tree). Reuse `ConfigurationService`'s query shape.
  2. Decide the text path **from the config**:
     - `DocumentModel == null` → local text extraction (see parsers below).
     - `DocumentModel != null` → Azure Document Intelligence analyze with `DocumentModel.ModelId`:
       - `prebuilt-layout` → concatenate line content into text.
       - custom model → serialize the returned `fields` dictionary to JSON text.
  3. Build the JSON Schema from the `MappedField` tree (`JsonSchemaBuilder`).
  4. Build the chat messages (`PromptBuilder`) using `Configuration.SystemPrompt` /
     `UserPromptTemplate` (fall back to legacy defaults), the document text, and the schema.
  5. Call the configured `LanguageModel` and parse the JSON response.
  6. Persist a `RunHistory` (StartedAt/CompletedAt, Success, RawInput = file name + text
     snippet, RawOutput = extracted JSON, DocumentsProcessed = 1, TriggeredBy = current user)
     via `IRunHistoryService`.
  7. Return `ExtractionResultDto`.
- `Parsing/IDocumentTextExtractor` + implementations `PdfTextExtractor`, `DocxTextExtractor`,
  `PlainTextExtractor`, selected by file extension (mirror legacy `DocumentParserFactory`).
- `DocumentIntelligenceTextProvider` — wraps the DI analyze call (port of
  `DocumentIntelligenceParser.TransformPrebuiltLayout` / `TransformCustomModel`, no page
  splitting for the MVP).
- `JsonSchemaBuilder` — port of legacy `DropSchema.ToJsonSchema()`, fed from `MappedField`
  (map `FieldType`→JSON type, `RequirementLevel`→required/nullable, `ExtractionHint`→description,
  nested `ChildFields`→object properties).
- `PromptBuilder` — port of legacy `PromptFactory.GenerateOpenAiChatMessages`.
- `LlmExtractor` — builds `ChatCompletionOptions` with strict structured output
  (`ChatResponseFormat.CreateJsonSchemaFormat`) and calls Azure OpenAI.

### Reuse (do not reinvent)

- **LLM client + key decryption**: copy the credential pattern from
  `Application/Services/LanguageModelService.cs` — `IDataProtectionProvider` protector
  (`"LanguageModel.ApiKey"`) + `AzureOpenAIClient` (key or `DefaultAzureCredential`).
- **DI credentials**: mirror `DocumentModelService` protector/credential handling.
- **Result/response envelope**: `FluentResults` + `Application/Common/ResultExtensions.cs`.
- **Mapster** for `Configuration`/`MappedField` projections if needed.

### New DTO

- `Application/DTOs/Extraction/ExtractionResultDto.cs`:
  `{ Fields: object (extracted JSON), DurationMs, LanguageModelName, DocumentModelName?,
     UsedDocumentIntelligence: bool, RunHistoryId, Warnings: string[] }`.

### Packages & registration

- Add to `Tyto.Api.csproj`: `Azure.AI.DocumentIntelligence` (DI path),
  `UglyToad.PdfPig` (local PDF text), `DocumentFormat.OpenXml` (DOCX text).
  `Azure.AI.OpenAI` and `Azure.Identity` are already referenced.
- Register `IExtractionService` + helpers in
  `Extensions/ServiceCollectionExtensions.cs::AddApplicationServices`. `AddDataProtection()`
  and `AddHttpClient()` are already there.

---

## Frontend — `tyto-admin-ui` (Angular 21)

### New feature `src/app/features/extraction/`

A standalone component (follow the structure of existing features like `configurations/`):

- **Config selector**: dropdown populated from
  `features/mappings/configurations.service.ts` (`ConfigurationsService.getAll()` already
  exists and unwraps the paged `ApiResponse`).
- **Drop zone**: minimal drag-&-drop + click-to-select (no existing upload component in
  `@innovation/ui`, so build a small one here). Enforce the selected config's accepted
  types/size client-side for UX.
- **Run button** → new `ExtractionService.run(configId, file)`: `multipart/form-data` POST
  to `${API_ROOT}/configurations/{id}/extract`, with loading state.
- **Results panel**: pretty-print the returned `fields` JSON as a key/value table plus a raw
  JSON view, show duration and which path (LLM-only vs Document Intelligence) was used.

### Wiring

- New `ExtractionService` (does not need full `BaseResourceService`; a thin `HttpClient`
  POST returning `ApiResponse<ExtractionResultDto>`).
- Add a route for the feature and a nav item in
  `src/app/core/navigation/shell-navigation.config.ts` (e.g. id `run`, label
  "Run Extraction", route `/app/extraction`), consistent with existing entries.

---

## Out of scope (explicitly deferred)

- Writing extracted data to the destination `DatabaseConnection` (Salesforce/Dataverse),
  lookups, child-record creation, default values, JS callbacks.
- Page classifier / multi-section / `multiple`-record aggregation (legacy classifier paths).
- Async/queue/batch execution and PDF page splitting.
- Image extraction.

---

## Verification

- **Backend unit tests** (`Tyto.Api.Tests`, follow `Mapping`/`Services` patterns):
  - `JsonSchemaBuilder` produces the expected JSON Schema from a nested `MappedField` tree
    (types, required, nullable, enum for Picklist).
  - File-extractor factory selects the right parser per extension.
- **Backend integration** (`Integration/`, `WebApplicationFactory<Program>`): `POST
  api/configurations/{id}/extract` with a small `.txt` and a config that uses only a
  `LanguageModel` → 200 with a `fields` object and a persisted `RunHistory`. The live LLM
  call is the one external dependency; gate it behind config or stub the LLM client for the
  automated test.
- **Manual end-to-end**: run the API (`dotnet run`), `ng serve` the admin UI, open the new
  page, pick an existing configuration, drop a sample PDF/TXT, click Run, and confirm the
  extracted JSON renders and a Run History row appears.

---

## Next steps after the POC (decoupling roadmap)

Once the slice works, evolve it toward the scalable target described in
[legacy-document-processing.md](./legacy-document-processing.md) §7:

1. Server-side **write to the destination** `DatabaseConnection` (the largest net-new piece).
2. **Async/batch** execution with `RunHistory` status tracking (queue, document storage).
3. **Classifier / multi-section** support (legacy `ClassifierConfig` + `SectionConfig`).
4. Replace ad-hoc JS callbacks with a safe, declarative server-side mapping step.
