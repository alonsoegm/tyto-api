# MVP Backend Plan — `POST /configurations/{id}/extract`

> **Status:** proposed plan (not yet implemented). This is the **baseline** to build and
> iterate from — if a step behaves differently than described, we adjust here first.
> **Related:** [mvp-extraction-poc.md](./mvp-extraction-poc.md) (overall POC),
> [legacy-document-processing.md](./legacy-document-processing.md) (engine we port from).
> **UI is already built** against this contract (`tyto-admin-ui`,
> `features/extraction/`) using a dev mock; flipping `USE_MOCK = false` wires it to this API.

## Context

The new Tyto API models every building block of an extraction pipeline and exposes CRUD,
but has **no execution engine**. This plan adds the smallest server-side slice that takes a
saved `Configuration` + an uploaded document, runs the extraction, and **returns the
structured JSON** (plus a persisted `RunHistory`). It does **not** write to the destination
database yet. The text path is **driven by the configuration**: Document Intelligence when a
`DocumentModel` is set, local text extraction otherwise; the `LanguageModel` always
normalizes the result to the schema built from `MappedField`s.

---

## The contract (must match the UI)

**Request:** `POST api/configurations/{id:guid}/extract`, `multipart/form-data`, field `file`.

**Response:** `ApiResponse<ExtractionResultDto>` where:

```jsonc
{
  "fields": { /* extracted data, shaped by MappedFields */ },
  "durationMs": 1832,
  "languageModelName": "gpt-4o",
  "documentModelName": null,            // or the DI model name
  "usedDocumentIntelligence": false,
  "runHistoryId": "<guid>",
  "warnings": ["..."]
}
```

(Matches `tyto-admin-ui/.../extraction/extraction.types.ts`.)

---

## Step-by-step flow

```
POST /configurations/{id}/extract  (file)
        │
        ▼
[1] Validate request ──► 400 ValidationError on bad/empty/oversized/unsupported file
        │
        ▼
[2] Load Configuration + includes (LanguageModel, DocumentModel, MappedFields tree)
        │                                   └─► 404 NotFound if missing
        ▼
[3] Choose path from config
        ├── DocumentModel == null ─────────► [4a] Local text extraction
        └── DocumentModel != null ─────────► [4b] Document Intelligence
        │
        ▼
[5] Build JSON Schema from MappedFields
        │
        ▼
[6] Build chat prompt (system + document + schema)
        │
        ▼
[7] Call the configured LanguageModel (strict structured output)
        │                                   └─► failure ► record failed RunHistory, 5xx
        ▼
[8] Parse + assemble ExtractionResultDto
        │
        ▼
[9] Persist RunHistory (success)
        │
        ▼
[10] 200 ApiResponse<ExtractionResultDto>
```

### [1] Validate the request
- `file` present and non-empty.
- Extension within `Configuration.AcceptedFileTypes` (fallback: `.pdf,.docx,.txt`).
- Size ≤ `Configuration.MaxUploadSizeMB`.
- **Out:** validated `IFormFile` + its bytes. **Errors:** `ValidationError` → RFC7807.

### [2] Load the configuration (with the field tree)
- Query `Configurations` **including** `LanguageModel`, `DocumentModel`, and
  `MappedFields` + their `ChildFields`.
- ⚠️ **Gotcha:** the existing `ConfigurationService.GetByIdAsync`
  (`Application/Services/ConfigurationService.cs:79`) includes the models but **not**
  `MappedFields`. The extraction service needs its **own** query that also includes the
  `MappedFields` tree (use `AsNoTracking`, order by `SortOrder`).
- **Out:** the `Configuration` aggregate. **Errors:** `NotFoundError`.

### [3] Choose the extraction path
- `Configuration.DocumentModel == null` → **local text** ([4a]).
- otherwise → **Document Intelligence** ([4b]).
- (`ExtractionStrategy` is `SingleModel`/`MultiModel`; the MVP handles `SingleModel` only —
  no classifier/multi-section.)

### [4a] Local text extraction
- Pick a parser by file extension (mirror legacy `DocumentParserFactory`):
  `PdfTextExtractor` (UglyToad.PdfPig), `DocxTextExtractor` (DocumentFormat.OpenXml),
  `PlainTextExtractor`.
- **Out:** `documentText: string`. `usedDocumentIntelligence = false`.

### [4b] Document Intelligence
- Build a `DocumentIntelligenceClient` from `DocumentModel` (`Endpoint`, `ModelId`,
  key via `_protector.Unprotect(...)` or managed identity — mirror
  `DocumentModelService` credential handling and its `CognitiveServicesTokenRequest`).
- Analyze the document with `ModelId`:
  - `prebuilt-layout` → concatenate page/line content into text.
  - custom model → serialize the returned `fields` dictionary (name → value) to JSON text.
- **Out:** `documentText: string` (text or serialized fields). `usedDocumentIntelligence = true`.

### [5] Build the JSON Schema (`JsonSchemaBuilder`)
- Port of legacy `DropSchema.ToJsonSchema()`. From the `MappedField` tree:
  - `FieldType` → JSON type (`Text`→string, `Number`/`Currency`→number, `Date`→string +
    format instruction, `Boolean`→boolean, `Picklist`→string + `enum`, `Lookup`→string).
  - `RequirementLevel` → `required` membership + nullability.
  - `ExtractionHint` → field `description`; `DisplayLabel`/`FieldName` → property name.
  - nested `ChildFields` → `object` (and array when the field represents a collection).
  - objects set `additionalProperties: false`.
- **Out:** a strict JSON Schema string for structured outputs.

### [6] Build the prompt (`PromptBuilder`)
- Port of legacy `PromptFactory.GenerateOpenAiChatMessages`. Three messages:
  1. **System:** `Configuration.SystemPrompt` (fallback to the legacy default — "document
     reading assistant… return only valid JSON… never guess… leave unfound fields null").
  2. **User:** the document text wrapped in `[BEGIN DOCUMENT] … [END DOCUMENT]`.
  3. **User:** "Provide this information as follows: `<schema>`".
  (`UserPromptTemplate` may override message 2/3 if present.)

### [7] Call the LanguageModel (`LlmExtractor`)
- Build `AzureOpenAIClient` from `LanguageModel` (decrypt `ApiKeyEncrypted` with the
  `"LanguageModel.ApiKey"` protector, or `DefaultAzureCredential`) — **reuse the exact
  pattern in `LanguageModelService.cs:327-361`** (`GetChatClient(DeploymentName)` →
  `CompleteChatAsync`).
- `ChatCompletionOptions`: `Temperature` + `MaxOutputTokens` from `Configuration`,
  `ResponseFormat = CreateJsonSchemaFormat(..., strict: true)` with the schema from [5].
- Handle `Refusal` → client-safe error.
- **Out:** raw JSON string → parsed object.

### [8] Assemble the result
- `fields` = parsed LLM JSON; `durationMs` = stopwatch from [1]; set `languageModelName`,
  `documentModelName`, `usedDocumentIntelligence`, `warnings`.

### [9] Persist `RunHistory`
- Create a `RunHistory`: `ConfigurationId`, `StartedAt`/`CompletedAt`, `Success`,
  `DocumentsProcessed = 1`, `RawInput` = file name + text snippet, `RawOutput` = extracted
  JSON, `TriggeredBy` = current user.
- ⚠️ **Gotcha:** `IRunHistoryService` has **no create method** today (only `GetAll`/
  `GetById`). Add a `RecordAsync`/`CreateAsync` (and a create DTO), or persist via
  `_db.RunHistories.Add(...)`. The `UseUnitOfWork` middleware commits once per request.
- On **failure** in [4]–[7], still persist a failed `RunHistory` with `ErrorMessage`.

### [10] Respond
- Success → `200 ApiResponse<ExtractionResultDto>` via `result.ToApiResponse()`.
- Failure → `result.ToErrorResult(this)` (RFC7807), consistent with existing controllers.

---

## Files to add / change (`Tyto.Api`)

- `Controllers/ConfigurationsController.cs` — add `POST {id:guid}/extract`
  (`[FromForm] IFormFile file`).
- `Application/Interfaces/IExtractionService.cs` + `Application/Services/Extraction/ExtractionService.cs` — orchestrator (steps 2–10).
- `Application/Services/Extraction/Parsing/` — `IDocumentTextExtractor` + `PdfTextExtractor`,
  `DocxTextExtractor`, `PlainTextExtractor` + a factory (step 4a).
- `Application/Services/Extraction/DocumentIntelligenceTextProvider.cs` (step 4b).
- `Application/Services/Extraction/JsonSchemaBuilder.cs` (step 5).
- `Application/Services/Extraction/PromptBuilder.cs` (step 6).
- `Application/Services/Extraction/LlmExtractor.cs` (step 7).
- `Application/DTOs/Extraction/ExtractionResultDto.cs`.
- `Application/Services/RunHistoryService.cs` (+ `IRunHistoryService`, + create DTO) — add the
  record/create method (step 9).
- `Extensions/ServiceCollectionExtensions.cs` — register `IExtractionService` + helpers.
- `Tyto.Api.csproj` — add `Azure.AI.DocumentIntelligence`, `UglyToad.PdfPig`,
  `DocumentFormat.OpenXml` (`Azure.AI.OpenAI` + `Azure.Identity` already referenced).

### Reuse (don't reinvent)
- LLM client + chat call: `LanguageModelService.cs:327-361`.
- DI credentials / token request: `DocumentModelService` (`CognitiveServicesTokenRequest`).
- Secret decryption: `IDataProtectionProvider` protectors `"LanguageModel.ApiKey"` /
  `"DocumentModel.ApiKey"`.
- Result/response envelope: `Application/Common/ResultExtensions.cs`, `ApiResponse`.
- Error types: `ValidationError`, `NotFoundError`, `InternalError` (`Application/Common/Errors/`).

---

## Out of scope (deferred)
- Writing to the destination `DatabaseConnection`; lookups; child-record creation; callbacks.
- Classifier / multi-section / `multiple`-record aggregation; PDF page splitting.
- Async/queue/batch; image extraction.

---

## Verification
- **Unit** (`Tyto.Api.Tests`): `JsonSchemaBuilder` over a nested `MappedField` tree
  (types, required, nullable, `enum` for Picklist); parser factory selection by extension.
- **Integration** (`WebApplicationFactory<Program>`): `POST .../extract` with a small `.txt`
  and an LLM-only configuration → 200 with a `fields` object + a persisted `RunHistory`.
  Stub the LLM client (or gate behind config) so the test doesn't call a live model.
- **End-to-end:** set `USE_MOCK = false` in the UI service, `dotnet run` + `ng serve`, pick a
  real configuration, drop a PDF/TXT, Run → JSON renders and a Run History row appears.

---

## Iteration notes (likely first adjustments)
This baseline will probably need tuning at these points — expected, not failures:
1. **Schema strictness** — OpenAI structured-output rejects certain shapes (formats on
   `date`, missing `required`, nullable encoding). Adjust `JsonSchemaBuilder` mappings first.
2. **`FieldType` → schema** for `Picklist`/`Lookup` (need option lists / target metadata the
   MVP doesn't model yet) — start with string + hint, refine later.
3. **DI model handling** — `prebuilt-layout` (text) vs custom model (fields map) branch.
4. **Secret decryption purpose-strings** — must exactly match the protector names used when
   the keys were encrypted, or `Unprotect` throws.
5. **Prompt defaults** — when `SystemPrompt`/`UserPromptTemplate` are empty, fall back to the
   legacy prompts; tune wording against real documents.
