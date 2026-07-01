namespace Tyto.Api.Domain.Entities;

/// <summary>
/// A completed extraction persisted to Tyto's internal Azure SQL destination. The structured output
/// is stored as JSON in <see cref="ExtractedData"/> (including any field-level confidence/source
/// provenance), associated with the configuration and run-history records that produced it.
/// </summary>
public class ExtractionResult : BaseEntity
{
    public Guid ConfigurationId { get; set; }
    public Configuration Configuration { get; set; } = null!;

    public Guid RunHistoryId { get; set; }
    public RunHistory RunHistory { get; set; } = null!;

    /// <summary>The extracted structured data as a JSON document.</summary>
    public string ExtractedData { get; set; } = string.Empty;

    public string LanguageModelName { get; set; } = string.Empty;
    public string? DocumentModelName { get; set; }
    public long DurationMs { get; set; }
}
