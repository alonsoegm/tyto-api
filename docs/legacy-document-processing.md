# Legacy Tyto — Document Processing Pipeline

> **Purpose of this document**
> This is a reverse-engineered map of how the **legacy Tyto** (the PCF + Azure
> Function solution) processes documents. It exists so we can decide what to **reuse**,
> what to **discard**, and how to **re-map** the extraction engine into the **new
> Tyto** — a configuration-driven app where Document Intelligence models, LLMs, and
> destination databases are configured up front, and a process runs over documents that
> are then stored in a destination.
>
> Source analyzed: `tyto-old/tyto/PDFExtractFunction/` (the backend) and
> `tyto-old/tyto/TytoControl/` (the PCF front end).

---

## 1. Two halves of the legacy system

```
┌──────────────────────────────┐        ┌────────────────────────────────────┐
│  TytoControl (PCF)        │  POST  │  PDFExtractFunction (Azure Function)│
│  React + Fluent UI, in        │ ─────▶ │  .NET 8 isolated worker             │
│  Dynamics 365 forms           │ multipart                                   │
│                               │        │  - Parse document (PDF/DOCX/TXT)    │
│  - Drag & drop upload         │        │  - Classify pages (Doc Intelligence)│
│  - MSAL / Entra auth          │        │  - Extract fields (Doc Int + LLM)   │
│  - Reads JS config WebResource│        │  - Return structured JSON           │
│  - Writes result to the form  │ ◀───── │                                     │
│    via Xrm Web API (client)   │  JSON  └────────────────────────────────────┘
└──────────────────────────────┘
```

The legacy split matters because **the two halves have very different fates in the new
product**:

| Half | What it did | Fate in new Tyto |
| --- | --- | --- |
| **PCF control** (`TytoControl/`) | Upload UI, auth, read config from a Dynamics JS WebResource, and **write results back to a Dynamics form record from the browser** using the Xrm Web API. | **Discard.** Replaced by a configuration app + a server-side execution process. There is no Dynamics form context anymore. |
| **Azure Function** (`PDFExtractFunction/`) | The actual **extraction engine**: parse → classify → extract → return JSON. | **Reuse / port.** This is the core logic we want to lift into the new processing service. |

Everything below focuses on the **Azure Function** because that is the part worth keeping.

---

## 2. The request that drives a run

The legacy engine is **stateless**: every call carries both the document **and** the full
configuration for how to process it. There is no stored configuration on the server side.

### 2.1 Transport
`TytoExtractFunction.Run` ([TytoExtractFunction.cs](../../tyto-old/tyto/PDFExtractFunction/PDFExtractFunction/TytoExtractFunction.cs)) is an
HTTP-triggered function (`POST`, function-key auth). It receives `multipart/form-data`:

- `File` — the uploaded document (`.pdf`, `.docx`, or `.txt`).
- `requestJson` — a JSON blob deserialized into `RequestPayload`.

`RequestInfo` ([RequestInfo.cs](../../tyto-old/tyto/PDFExtractFunction/PDFExtractFunction/Model/RequestInfo.cs)) infers the document type from the file extension and wraps the bytes
in a `DocumentWrapper`.

### 2.2 The per-request payload (`RequestPayload`)
This is the **runtime config the engine needs to do one extraction**:

```
RequestPayload
├── extractImages : bool                 // also pull images out of the doc
├── classifier    : ClassifierConfig?    // null = single-section mode
│     ├── type               : DocIntClassifier | DocIntComposite | DocIntField
│     ├── modelName          : string
│     ├── fieldName          : string?           // for DocIntField
│     ├── ignoredDocumentTypes: string[]
│     └── typesByLabel       : { label -> documentType }   // for DocIntField
├── sections      : SectionConfig[]
│     ├── documentType   : string                 // matches a classifier output
│     ├── textExtraction : { strategy, modelName }?// PlainText | DocumentIntelligence
│     ├── multiple       : bool                    // one record vs many (child records)
│     ├── arrayFields    : string[]                // fields aggregated when multiple
│     └── fields         : DropSchema[]            // the field tree to extract
└── llmSettings   : { deploymentName?, generalInstructions? }
```

`DropSchema` ([DropSchema.cs](../../tyto-old/tyto/PDFExtractFunction/PDFExtractFunction/Model/DropSchema.cs)) is the recursive field definition — type, nullability, nested
`fields`, `validValues` (enums), `promptName`, and `customInstructions`. **It compiles
directly into a JSON Schema** for the LLM's structured-output response (see §5).

> **Mapping note:** in the new product these no longer arrive per request. They are the
> stored `Configuration` + `MappedField` + `DocumentModel` + `LanguageModel` entities.
> See §7.

### 2.3 Environment-level config (`TytoConfig`)
Separate from the per-request payload, `TytoConfig` ([TytoConfig.cs](../../tyto-old/tyto/PDFExtractFunction/PDFExtractFunction/Model/TytoConfig.cs)) reads
**deployment settings** from app configuration: Azure OpenAI URL / model / key /
temperature, Document Intelligence endpoint / key, **confidence thresholds**
(`AnalyzeMinConfidence`, `ClassifyMinConfidence`), retry attempts, and debug toggles.

Auth to both Azure OpenAI and Document Intelligence supports **key OR managed identity**
(`"default"` ⇒ `DefaultAzureCredential`) — see [TytoServices.cs](../../tyto-old/tyto/PDFExtractFunction/PDFExtractFunction/TytoServices.cs).

> **Mapping note:** these endpoint/credential settings are exactly what the new
> `LanguageModel` and `DocumentModel` connection entities will hold — but **per
> configuration**, not per deployment.

---

## 3. End-to-end processing flow

Orchestrated by `RequestProcessor.ProcessRequest` ([RequestProcessor.cs](../../tyto-old/tyto/PDFExtractFunction/PDFExtractFunction/Processing/RequestProcessor.cs)).

```
                         ┌─────────────────────────────────────────┐
  multipart request ───▶ │ RequestInfo: file + RequestPayload      │
                         └─────────────────────────────────────────┘
                                          │
                       classifier == null │ classifier != null
              ┌───────────────────────────┴───────────────────────────┐
              ▼                                                         ▼
   ┌─────────────────────┐                       ┌──────────────────────────────────────┐
   │ SINGLE SECTION      │                       │ CLASSIFY DOCUMENT                      │
   │ exactly 1 section   │                       │ Doc Intelligence: classify each page  │
   │ required            │                       │  → list of {pages, docType, conf}     │
   └─────────┬───────────┘                       └──────────────────┬───────────────────┘
             │                                                       │
             │                                  group pages by docType, then per group:
             │                                   - drop ignored / unknown types
             │                                   - filter below ClassifyMinConfidence
             │                                   - multiple? keep all : keep top-confidence
             │                                                       │
             │                                          match group → SectionConfig
             │                                                       │
             └──────────────────────────┬────────────────────────────┘
                                         ▼
                          ┌──────────────────────────────┐
                          │ SectionProcessor.Process      │   (per section / per page)
                          │  strategy switch:             │
                          │   • PlainText → text → LLM    │
                          │   • DocumentIntelligence:     │
                          │       - prebuilt-layout → text→LLM
                          │       - custom model → fields → LLM
                          └──────────────┬────────────────┘
                                         ▼
                          ┌──────────────────────────────┐
                          │ LLM structured extraction     │
                          │ (JSON Schema strict output)   │
                          └──────────────┬────────────────┘
                                         ▼
                          ┌──────────────────────────────┐
                          │ Merge all section JObjects    │
                          │ (deep merge; nulls don't      │
                          │  overwrite earlier values)    │
                          └──────────────┬────────────────┘
                                         ▼
                          FunctionResponse { fields, images, devMessages }
```

### 3.1 Document ingestion & PDF splitting
`DocumentWrapper` ([DocumentWrapper.cs](../../tyto-old/tyto/PDFExtractFunction/PDFExtractFunction/Helpers/DocumentWrapper.cs)) holds the raw bytes and lazily exposes:
- a `PdfDocument` (PdfPig) for page counting,
- a `PdfSplitter` (PdfSharp) to extract **specific pages as their own PDF stream**.

Why split? Document Intelligence *can* take a `pages` parameter, but the legacy code
found it **faster to physically split the PDF and upload only the needed pages** rather
than upload the whole document and select pages server-side
(see comment in [DocumentIntelligenceParser.Analyze](../../tyto-old/tyto/PDFExtractFunction/PDFExtractFunction/Parsers/DocumentIntelligenceParser.cs)).

Parsers are chosen by a small factory ([DocumentParserFactory.cs](../../tyto-old/tyto/PDFExtractFunction/PDFExtractFunction/Parsers/DocumentParserFactory.cs)):

| Document type | Parser | Used for |
| --- | --- | --- |
| `.pdf` | `PDFDocumentParser` (PdfPig) | plain-text extraction + image extraction |
| `.docx` | `DocxDocumentParser` (OpenXml) | plain-text extraction |
| `.txt` | `PlainTextDocumentParser` | pass-through |

### 3.2 Classification (only when `classifier` is set)
`DocumentIntelligenceParser.ClassifyDocument` ([DocumentIntelligenceParser.cs](../../tyto-old/tyto/PDFExtractFunction/PDFExtractFunction/Parsers/DocumentIntelligenceParser.cs)) supports **three classifier
strategies** (`ClassifierType`):

1. **`DocIntClassifier`** — Document Intelligence *classifier* model, split `PerPage`.
   Each page gets a `documentType` + confidence.
2. **`DocIntComposite`** — a composite *analyze* model that both classifies and returns
   fields in one call.
3. **`DocIntField`** — analyze each page with a normal model, read a **specific field**
   (`fieldName`) whose value labels the page. The label is translated to a document type
   via `typesByLabel`. If the OCR'd label isn't an exact match, an **LLM OCR-correction**
   step (`LlmService.OcrCorrection`) picks the closest valid label.

Results below `ClassifyMinConfidence` are dropped. Then `RequestProcessor`:
- groups pages by `documentType`,
- **ignores** unknown types and `ignoredDocumentTypes` (recording a dev message),
- throws if a detected type has **no matching `SectionConfig`**,
- for `multiple == false`: keeps only the **highest-confidence** page and discards the rest,
- for `multiple == true`: processes **every** page and aggregates results into arrays
  (`arrayFields`) — this is how one-record-per-page (child records) works.

### 3.3 Extraction strategies (per section)
`SectionProcessor` ([SectionProcessor.cs](../../tyto-old/tyto/PDFExtractFunction/PDFExtractFunction/Processing/SectionProcessor.cs)) resolves the `ExtractionStrategy`:

| Strategy | Pipeline | What goes to the LLM |
| --- | --- | --- |
| **PlainText** | Parse doc → raw text | full text of the page(s) |
| **DocumentIntelligence + `prebuilt-layout`** | Analyze → collect line content | serialized lines (text) |
| **DocumentIntelligence + custom model** | Analyze → `Fields` dict (above `AnalyzeMinConfidence`) | serialized field name → value map |
| **Composite classifier fields** | fields already returned by the composite classify call | serialized field map |

In **every** strategy the final structuring is done by the **LLM**, not by Document
Intelligence directly. Document Intelligence is used for OCR/layout/field detection;
the LLM normalizes those into the requested schema. There's a `// TODO` noting that
simple fields (strings, dates) *could* skip the LLM — they don't today.

### 3.4 LLM structured extraction
`PromptFactory.GenerateOpenAiChatMessages` ([PromptFactory.cs](../../tyto-old/tyto/PDFExtractFunction/PDFExtractFunction/Helpers/PromptFactory.cs)) builds three messages:

1. **System**: "You are a document reading assistant… return only well-formed JSON…
   never guess… leave unfound fields null" + any `generalInstructions`.
2. **User**: the document text/field map wrapped in `[BEGIN DOCUMENT] … [END DOCUMENT]`.
3. **User**: "Please provide this information as follows: `<JSON Schema>`".

The schema is generated by `DropSchema.ToJsonSchema()` and enforced with
**OpenAI Structured Outputs** (`ChatResponseFormat.CreateJsonSchemaFormat(..., strict: true)`).
Notable schema behaviors:
- `date` / `date-time` become `string` with explicit format instructions injected into the
  field `description` (structured outputs don't support those formats natively).
- `validValues` ⇒ JSON `enum`; nullable fields add `"null"` to the type array / enum.
- objects set `additionalProperties: false` and list all child fields as `required`.
- temperature comes from `TytoConfig` (default 0); a `Refusal` from the model is
  surfaced as a client-safe error.

`LlmService` ([LlmService.cs](../../tyto-old/tyto/PDFExtractFunction/PDFExtractFunction/Helpers/LlmService.cs)) calls Azure OpenAI and parses the returned JSON.

### 3.5 Merge & response
Each section yields a `JObject`. `RequestProcessor` **deep-merges** them so child-record
fields from different sections combine, with one guard: **a later `null` never overwrites
an earlier non-null value**. The function returns `FunctionResponse`
([FunctionResponse.cs](../../tyto-old/tyto/PDFExtractFunction/PDFExtractFunction/Model/FunctionResponse.cs)):

```
FunctionResponse
├── fields      : JObject                       // the extracted data
├── images      : ImageAttachment[]             // when extractImages = true
└── devMessages : { key -> string[] }           // per-page diagnostics / troubleshooting
```

> **Key observation:** the legacy engine **stops at "structured JSON"**. It does **not**
> write to any database. All persistence (mapping to a Dynamics record, creating child
> records, resolving lookups, default values, callbacks) happened **client-side in the PCF
> control** via the Xrm Web API. That responsibility has to move **server-side** in the
> new product.

---

## 4. Cross-cutting behaviors worth keeping

- **Confidence gating** at two levels (classify + analyze) — cheap quality control.
- **Dev messages**: a structured, per-page diagnostic channel
  ([DevMessagesManager](../../tyto-old/tyto/PDFExtractFunction/PDFExtractFunction/Helpers/DevMessagesManager.cs)) returned to the caller for troubleshooting classifier/extraction
  issues. Maps naturally onto the new `RunHistory` / `AuditLog`.
- **Client-safe vs internal errors**: `ClientSafeDetailsException` carries user-facing
  details; everything else is hidden unless `ReturnUnknownExceptionDetails` is on.
- **Parallelism**: sections/pages are processed concurrently (`Task.WhenAll`).
- **LLM OCR correction**: a reusable trick for fuzzy label/value matching.
- **Key-or-managed-identity** auth for both Azure services.

---

## 5. Configuration model: legacy → new entities

The single biggest change: the legacy **per-request `RequestPayload`** becomes **stored,
reusable configuration** in the new app. Rough correspondence:

| Legacy concept (per-request) | New Tyto entity | Notes |
| --- | --- | --- |
| `TytoConfig` OpenAI settings + `llmSettings` | **`LanguageModel`** | endpoint, deployment, key/MI, temperature, general instructions. Now stored & selectable (`ModelSelectionMode`). |
| `ExtractionStrategyConfig` / `ClassifierConfig` (modelName, type) | **`DocumentModel`** | Document Intelligence model + classifier metadata. `ExtractionStrategy` enum already exists in new Domain. |
| `RequestPayload` as a whole (classifier + sections + llmSettings) | **`Configuration`** | the named, reusable processing recipe. |
| `SectionConfig` | part of **`Configuration`** (sections) | section = documentType + strategy + fields. |
| `DropSchema` field tree | **`MappedField`** | type (`FieldType`), `RequirementLevel`, prompt name, custom instructions, nesting. |
| Dynamics form record (Xrm, client-side) | **`DatabaseConnection`** | the **destination** — Salesforce / Dataverse — now written **server-side**. |
| `FunctionResponse.devMessages` | **`RunHistory`** / **`AuditLog`** | execution outcome + diagnostics, persisted. |

The new `Domain/Enums` already mirror legacy ideas: `ExtractionStrategy`, `FieldType`,
`RequirementLevel`, `ModelSelectionMode`, `ConnectionType`, plus auth enums for the
destinations.

---

## 6. What to reuse, adapt, and discard

### Reuse (port the logic ~as-is)
- Document ingestion + **PDF page splitting** (`DocumentWrapper`, `PdfSplitter`, parsers).
- The **classify → group → pick/aggregate → extract** orchestration in `RequestProcessor`.
- The **strategy switch** in `SectionProcessor` (PlainText / Doc Intelligence / composite).
- **`DropSchema.ToJsonSchema()`** + structured-output prompting in `PromptFactory`. This is
  the heart of the extraction and is provider-agnostic in shape.
- Confidence gating, dev-message diagnostics, client-safe error model, LLM OCR correction.

### Adapt
- **Configuration source**: stop reading config from the request; **load the stored
  `Configuration`** (with its `DocumentModel` / `LanguageModel` / `MappedField` /
  `DatabaseConnection`) by id.
- **Credentials**: per-`Configuration` connections instead of a single deployment's app
  settings.
- **Output**: replace "return JSON to the browser" with a **server-side writer to the
  destination `DatabaseConnection`** (Salesforce / Dataverse). All the client-side PCF
  logic — metadata-driven field mapping, child-record creation, lookup resolution,
  default values, `onValuesRetrieved` / `resolveMapping` callbacks — must be **re-homed
  server-side** (and the JS-callback escape hatch needs a server-side equivalent or
  replacement).

### Discard
- The entire **PCF control** (`TytoControl/`): React UI, MSAL-in-browser, WebResource
  config loading, Xrm client-side writes, Dynamics form context.
- **Dynamics WebResource JS config** delivery mechanism.
- The function-key HTTP transport assumption (replace with the new app's auth model).

---

## 7. Gaps the new design must answer (not in the legacy)

The legacy engine was synchronous, stateless, and stopped at JSON. The new model
("configure, then run a process over documents stored in a destination") introduces
concerns the old code never handled:

1. **Document storage / ingestion source** — where do documents come from and where are
   they stored (the "destination" for the documents themselves vs. the destination DB for
   extracted data)? Legacy only had an in-memory upload.
2. **Async / batch execution** — legacy was one synchronous HTTP call per document. A
   "run a process over documents" model likely needs queuing, batching, and
   `RunHistory` tracking of status.
3. **Server-side persistence to the destination** — the largest net-new piece (see §6
   *Adapt*): mapping extracted JSON to records, child records, lookups, and defaults
   **without** a browser/Xrm context.
4. **Per-configuration secrets** — endpoints/keys move from deployment settings to stored,
   encrypted connection entities (the new API already encrypts secrets at rest).
5. **Replacement for JS callbacks** — legacy allowed arbitrary client-side JS
   (`onValuesRetrieved`, `resolveMapping`). The new server-side process needs a safe,
   declarative (or sandboxed) equivalent.

---

## 8. TL;DR

The legacy Tyto's **extraction engine** (parse → optionally classify by page →
extract per section via Document Intelligence and/or plain text → normalize to a strict
JSON Schema with an LLM → deep-merge) is solid and worth **porting wholesale**. The parts
tied to **PCF + Dynamics** (the UI, the WebResource config delivery, and especially the
**client-side write-back to the form**) are **discarded** and must be rebuilt as a
**stored-configuration model + a server-side process that writes to a destination
database**. The new repo's entities (`Configuration`, `DocumentModel`, `LanguageModel`,
`MappedField`, `DatabaseConnection`, `RunHistory`) already line up with the legacy
concepts — the work is wiring the ported engine to read those entities and to persist
results server-side.
