using System.Diagnostics;
using System.Text.Json.Nodes;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Tyto.Api.Application.Common.Errors;
using Tyto.Api.Application.DTOs.Extraction;
using Tyto.Api.Application.Interfaces;
using Tyto.Api.Application.Services.Extraction.Parsing;
using Tyto.Api.Domain.Entities;
using Tyto.Api.Infrastructure.Data;
using ExtractionResultData = Tyto.Api.Application.DTOs.Extraction.ExtractionResult;
using ExtractionResultEntity = Tyto.Api.Domain.Entities.ExtractionResult;

namespace Tyto.Api.Application.Services.Extraction;

/// <summary>
/// Orchestrates a single extraction run: load configuration → extract text → build schema
/// and prompt → call the language model → write the result to the configured destination via the
/// resolved <see cref="IExtractionSink"/> → persist run history → return the JSON. This MVP implements
/// the LLM-only path (no Document Intelligence).
/// </summary>
public class ExtractionService : IExtractionService
{
    private static readonly string[] DefaultAcceptedExtensions = { ".pdf", ".docx", ".txt" };

    private readonly TytoDbContext _db;
    private readonly DocumentTextExtractorFactory _textExtractors;
    private readonly LlmExtractor _llmExtractor;
    private readonly IExtractionSinkResolver _sinkResolver;
    private readonly ILogger<ExtractionService> _logger;

    public ExtractionService(
        TytoDbContext db,
        DocumentTextExtractorFactory textExtractors,
        LlmExtractor llmExtractor,
        IExtractionSinkResolver sinkResolver,
        ILogger<ExtractionService> logger)
    {
        _db = db;
        _textExtractors = textExtractors;
        _llmExtractor = llmExtractor;
        _sinkResolver = sinkResolver;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<ExtractionResultDto>> ExtractAsync(
        Guid configurationId,
        ExtractionFileInput file,
        string triggeredBy,
        CancellationToken cancellationToken = default)
    {
        // [2] Load the configuration with its models and the mapped-field tree.
        // Identity resolution de-duplicates entities by key and fixes up navigations.
        var configuration = await _db.Configurations
            .AsNoTrackingWithIdentityResolution()
            .Include(c => c.LanguageModel)
            .Include(c => c.DocumentModel)
            .Include(c => c.DatabaseConnection)
            .Include(c => c.MappedFields)
            .FirstOrDefaultAsync(c => c.Id == configurationId, cancellationToken);

        if (configuration is null)
            return Result.Fail(new NotFoundError(nameof(Configuration), configurationId));

        if (configuration.LanguageModel is null)
            return Result.Fail(new ValidationError("The configuration has no language model assigned."));

        if (configuration.DatabaseConnection is null)
            return Result.Fail(new ValidationError("The configuration has no database connection assigned."));

        // [1] Validate the upload against the configuration.
        var validationError = ValidateFile(configuration, file);
        if (validationError is not null)
            return Result.Fail(validationError);

        // Resolve the destination sink up front so unsupported types (e.g. Cosmos DB) fail fast,
        // before any language-model work — and never after reporting success.
        var sinkResult = _sinkResolver.Resolve(configuration.DatabaseConnection);
        if (sinkResult.IsFailed)
            return Result.Fail(sinkResult.Errors);
        var sink = sinkResult.Value;

        var startedAt = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        var warnings = new List<string>();

        try
        {
            // [4a] LLM-only path: extract document text locally.
            var documentText = _textExtractors.ExtractText(file.FileName, file.Content);
            if (string.IsNullOrWhiteSpace(documentText))
                warnings.Add("No text could be extracted from the document; the result may be empty.");

            // [5] Build the strict JSON schema from the mapped-field tree.
            var topLevelFields = BuildFieldTree(configuration.MappedFields);
            if (topLevelFields.Count == 0)
                warnings.Add("The configuration has no mapped fields; nothing to extract.");
            if (configuration.MappedFields
                    .GroupBy(f => (f.ParentFieldId, f.FieldName))
                    .Any(group => group.Count() > 1))
            {
                warnings.Add("The configuration has duplicate mapped-field names; only the first of each was used.");
            }
            var schemaJson = JsonSchemaBuilder.Build(topLevelFields);

            // [6] Build the prompt and [7] call the language model.
            var messages = PromptBuilder.Build(configuration, documentText, schemaJson);
            var rawJson = await _llmExtractor.ExtractJsonAsync(
                configuration.LanguageModel, configuration, messages, schemaJson, cancellationToken);

            // [8] Parse the structured result.
            var fields = JsonNode.Parse(rawJson) as JsonObject ?? new JsonObject();
            stopwatch.Stop();

            // [9] Write the structured result to the configured destination, then persist the run.
            // The run is only recorded as successful once the write staged cleanly; unsupported
            // destinations throw and are handled below as a failed run.
            var run = CreateRun(configuration.Id, startedAt, success: true, file, rawJson, triggeredBy, error: null);
            var extraction = new ExtractionResultData(
                ConfigurationId: configuration.Id,
                RunHistoryId: run.Id,
                Fields: fields,
                LanguageModelName: configuration.LanguageModel.Name,
                DocumentModelName: null,
                DurationMs: stopwatch.ElapsedMilliseconds);
            await sink.WriteAsync(extraction, cancellationToken);
            run.RecordsCreated = 1;

            _db.RunHistories.Add(run);
            await _db.SaveChangesAsync(cancellationToken);

            // [10] Return the result.
            return Result.Ok(new ExtractionResultDto(
                Fields: fields,
                DurationMs: stopwatch.ElapsedMilliseconds,
                LanguageModelName: configuration.LanguageModel.Name,
                DocumentModelName: null,
                UsedDocumentIntelligence: false,
                RunHistoryId: run.Id,
                Warnings: warnings));
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Extraction failed for configuration {ConfigurationId}", configurationId);

            // A failed write may have staged a (now-invalid) success run and/or result — drop them so
            // only the failed run is persisted.
            DiscardPendingRunAndResult();

            // [9] Persist a failed run so the attempt is auditable, then surface the error.
            var detail = Describe(ex);
            var run = CreateRun(configuration.Id, startedAt, success: false, file, rawOutput: null, triggeredBy, error: detail);
            _db.RunHistories.Add(run);
            await _db.SaveChangesAsync(cancellationToken);

            // An unimplemented destination (external sinks in the MVP) is a controlled, expected
            // condition — surface it as a validation error rather than a 500.
            if (ex is NotSupportedException)
                return Result.Fail(new ValidationError(
                    $"The destination for this configuration is not available yet. {ex.Message}"));

            // NOTE: includes the exception detail in the response to speed up POC iteration.
            // Tighten this (generic message only) before any non-dev use.
            return Result.Fail(new InternalError($"Failed to run the extraction. {detail}", ex));
        }
    }

    /// <summary>
    /// Detaches any run-history or extraction-result rows staged during a failed attempt so the
    /// subsequent failed-run insert is the only change committed for this request.
    /// </summary>
    private void DiscardPendingRunAndResult()
    {
        var pending = _db.ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added &&
                        (e.Entity is RunHistory || e.Entity is ExtractionResultEntity))
            .ToList();

        foreach (var entry in pending)
            entry.State = EntityState.Detached;
    }

    private ValidationError? ValidateFile(Configuration configuration, ExtractionFileInput file)
    {
        if (file.Content.Length == 0)
            return new ValidationError("The uploaded document is empty.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var accepted = ParseAcceptedExtensions(configuration.AcceptedFileTypes);
        if (!accepted.Contains(extension))
            return new ValidationError(
                $"File type '{extension}' is not accepted by this configuration. Accepted: {string.Join(", ", accepted)}.");

        if (!_textExtractors.IsSupported(file.FileName))
            return new ValidationError($"File type '{extension}' cannot be processed yet.");

        if (configuration.MaxUploadSizeMB > 0)
        {
            var maxBytes = (long)configuration.MaxUploadSizeMB * 1024 * 1024;
            if (file.Length > maxBytes)
                return new ValidationError($"The document exceeds the maximum size of {configuration.MaxUploadSizeMB} MB.");
        }

        return null;
    }

    private static readonly char[] AcceptedTypeSeparators = { ',', ';', ' ', '\t', '\n', '\r' };

    /// <summary>
    /// Parses the configuration's accepted file types into a set of normalized extensions.
    /// Tolerates the formats it may be stored in: a JSON array (<c>["pdf","docx"]</c>),
    /// a delimited list, with or without leading dots.
    /// </summary>
    private static IReadOnlyCollection<string> ParseAcceptedExtensions(string? acceptedFileTypes)
    {
        if (string.IsNullOrWhiteSpace(acceptedFileTypes))
            return DefaultAcceptedExtensions;

        var normalized = acceptedFileTypes
            .Split(AcceptedTypeSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeExtension)
            .Where(extension => extension.Length > 1)
            .ToHashSet();

        return normalized.Count > 0 ? normalized : DefaultAcceptedExtensions;
    }

    /// <summary>Strips JSON/quote/dot decoration from a token and returns a ".ext" form.</summary>
    private static string NormalizeExtension(string token)
    {
        var cleaned = token.Trim().Trim('[', ']', '"', '\'', '.', ' ').ToLowerInvariant();
        return cleaned.Length == 0 ? string.Empty : $".{cleaned}";
    }

    /// <summary>
    /// Rebuilds the parent/child field hierarchy from the flat collection. EF's
    /// <c>AsNoTracking</c> does not fix up the self-referencing navigation, so we wire it
    /// up explicitly and return the top-level fields.
    /// </summary>
    private static IReadOnlyList<MappedField> BuildFieldTree(ICollection<MappedField> allFields)
    {
        var byParent = allFields.ToLookup(f => f.ParentFieldId);
        foreach (var field in allFields)
            field.ChildFields = byParent[field.Id].OrderBy(c => c.SortOrder).ToList();

        return byParent[null].OrderBy(f => f.SortOrder).ToList();
    }

    private static RunHistory CreateRun(
        Guid configurationId, DateTime startedAt, bool success,
        ExtractionFileInput file, string? rawOutput, string triggeredBy, string? error)
    {
        return new RunHistory
        {
            ConfigurationId = configurationId,
            StartedAt = startedAt,
            CompletedAt = DateTime.UtcNow,
            Success = success,
            ErrorMessage = error,
            DocumentsProcessed = 1,
            RecordsCreated = 0,
            RecordsUpdated = 0,
            RecordsFailed = success ? 0 : 1,
            RawInput = file.FileName,
            RawOutput = rawOutput,
            TriggeredBy = triggeredBy,
            CreatedBy = triggeredBy,
            UpdatedBy = triggeredBy,
        };
    }

    /// <summary>Builds a compact, human-readable description of an exception and its inner cause.</summary>
    private static string Describe(Exception ex)
    {
        var message = $"{ex.GetType().Name}: {ex.Message}";
        if (ex.InnerException is not null)
            message += $" -> {ex.InnerException.GetType().Name}: {ex.InnerException.Message}";
        return message;
    }
}
