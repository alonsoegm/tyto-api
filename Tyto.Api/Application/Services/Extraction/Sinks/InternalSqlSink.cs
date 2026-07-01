using Tyto.Api.Application.DTOs.Extraction;
using Tyto.Api.Application.Interfaces;
using Tyto.Api.Infrastructure.Data;
using ExtractionResultEntity = Tyto.Api.Domain.Entities.ExtractionResult;

namespace Tyto.Api.Application.Services.Extraction.Sinks;

/// <summary>
/// Extraction sink for Tyto's system-managed Azure SQL destination. Persists results into the
/// <c>ExtractionResults</c> table via the ambient <see cref="TytoDbContext"/>.
/// </summary>
public class InternalSqlSink : IExtractionSink
{
    /// <summary>The single logical entity this sink writes to.</summary>
    public const string EntityName = "ExtractionResults";

    private readonly TytoDbContext _db;
    private readonly ILogger<InternalSqlSink> _logger;

    public InternalSqlSink(TytoDbContext db, ILogger<InternalSqlSink> logger)
    {
        _db = db;
        _logger = logger;
    }

    public string ConnectionType => Domain.Enums.ConnectionType.InternalSql.ToString();

    /// <summary>
    /// Stages the extraction result for insertion into <c>ExtractionResults</c>. The write participates
    /// in the request-scoped Unit of Work — it does not call SaveChanges or open its own transaction.
    /// </summary>
    public Task WriteAsync(ExtractionResult result, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var entity = new ExtractionResultEntity
        {
            ConfigurationId = result.ConfigurationId,
            RunHistoryId = result.RunHistoryId,
            ExtractedData = result.Fields.ToJsonString(),
            LanguageModelName = result.LanguageModelName,
            DocumentModelName = result.DocumentModelName,
            DurationMs = result.DurationMs,
            CreatedBy = "system",
            UpdatedBy = "system"
        };

        _db.ExtractionResults.Add(entity);

        // No secrets or document content are logged — only correlation identifiers.
        _logger.LogInformation(
            "Staged extraction result for configuration {ConfigurationId}, run {RunHistoryId}",
            result.ConfigurationId, result.RunHistoryId);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Returns the fixed internal entity. No network or metadata request is performed.
    /// </summary>
    public Task<IEnumerable<string>> GetEntitiesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IEnumerable<string>>(new[] { EntityName });
}
