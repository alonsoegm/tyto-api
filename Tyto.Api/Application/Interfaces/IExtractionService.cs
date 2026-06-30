using Tyto.Api.Application.DTOs.Extraction;
using FluentResults;

namespace Tyto.Api.Application.Interfaces;

/// <summary>
/// Runs a saved configuration's extraction pipeline against an uploaded document.
/// The MVP returns the extracted JSON only — it does not write to the destination database.
/// </summary>
public interface IExtractionService
{
    /// <summary>Extracts structured data from <paramref name="file"/> using the configuration with the given id.</summary>
    /// <param name="configurationId">The configuration to run.</param>
    /// <param name="file">The uploaded document.</param>
    /// <param name="triggeredBy">Identity that triggered the run (for run history).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<ExtractionResultDto>> ExtractAsync(
        Guid configurationId,
        ExtractionFileInput file,
        string triggeredBy,
        CancellationToken cancellationToken = default);
}
