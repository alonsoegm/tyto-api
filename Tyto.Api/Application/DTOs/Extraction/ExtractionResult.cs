using System.Text.Json.Nodes;

namespace Tyto.Api.Application.DTOs.Extraction;

/// <summary>
/// The completed outcome of an extraction run, handed to an <c>IExtractionSink</c> for persistence to
/// the configured destination. Carries the structured data plus the identifiers and execution metadata
/// a sink needs to associate the result with its configuration and run-history records.
/// </summary>
/// <param name="ConfigurationId">The configuration that produced this result.</param>
/// <param name="RunHistoryId">The run-history record this result belongs to.</param>
/// <param name="Fields">The structured data extracted from the document (including any nested field provenance).</param>
/// <param name="LanguageModelName">Name of the language model used to normalize the result.</param>
/// <param name="DocumentModelName">Name of the Document Intelligence model used, or null for the LLM-only path.</param>
/// <param name="DurationMs">Total processing time, in milliseconds.</param>
public record ExtractionResult(
    Guid ConfigurationId,
    Guid RunHistoryId,
    JsonObject Fields,
    string LanguageModelName,
    string? DocumentModelName,
    long DurationMs);
