using System.Text.Json.Nodes;

namespace Tyto.Api.Application.DTOs.Extraction;

/// <summary>
/// Result of running an extraction for a saved configuration against a single document.
/// The MVP only returns the extracted JSON and run metadata — it does not write to the
/// destination database.
/// </summary>
/// <param name="Fields">The structured data extracted from the document, shaped by the configuration's mapped fields.</param>
/// <param name="DurationMs">Total processing time, in milliseconds.</param>
/// <param name="LanguageModelName">Name of the language model used to normalize the result.</param>
/// <param name="DocumentModelName">Name of the Document Intelligence model used, or null when the LLM-only path ran.</param>
/// <param name="UsedDocumentIntelligence">Whether the run used Azure Document Intelligence before the LLM.</param>
/// <param name="RunHistoryId">Identifier of the persisted run-history record.</param>
/// <param name="Warnings">Non-fatal diagnostics worth showing to the user.</param>
public record ExtractionResultDto(
    JsonObject Fields,
    long DurationMs,
    string LanguageModelName,
    string? DocumentModelName,
    bool UsedDocumentIntelligence,
    Guid RunHistoryId,
    IReadOnlyList<string> Warnings);
