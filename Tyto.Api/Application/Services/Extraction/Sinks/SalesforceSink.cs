using Tyto.Api.Application.DTOs.Extraction;
using Tyto.Api.Application.Interfaces;

namespace Tyto.Api.Application.Services.Extraction.Sinks;

/// <summary>
/// Extraction sink for Salesforce destinations. Registered so the pipeline can resolve it by
/// connection type; the write/metadata behavior is delivered by a dedicated Salesforce story and is
/// not part of the Internal SQL MVP.
/// </summary>
public class SalesforceSink : IExtractionSink
{
    public string ConnectionType => Domain.Enums.ConnectionType.Salesforce.ToString();

    public Task WriteAsync(ExtractionResult result, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("The Salesforce extraction sink is not implemented for the MVP.");

    public Task<IEnumerable<string>> GetEntitiesAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("The Salesforce extraction sink is not implemented for the MVP.");
}
