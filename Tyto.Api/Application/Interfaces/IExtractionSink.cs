using Tyto.Api.Application.DTOs.Extraction;

namespace Tyto.Api.Application.Interfaces;

/// <summary>
/// A destination the extraction pipeline can write results to. One implementation exists per
/// supported connection type; the pipeline resolves the right sink via <see cref="IExtractionSinkResolver"/>
/// and never contains destination-specific write logic.
/// </summary>
public interface IExtractionSink
{
    /// <summary>
    /// The connection type this sink handles. Matches the <c>ConnectionType</c> enum name and the
    /// key under which the sink is registered in dependency injection.
    /// </summary>
    string ConnectionType { get; }

    /// <summary>
    /// Writes a completed extraction result to the destination. Implementations honor the ambient
    /// Unit of Work and do not open or commit their own transaction.
    /// </summary>
    Task WriteAsync(ExtractionResult result, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the entities (tables/objects) this destination can receive data into.
    /// </summary>
    Task<IEnumerable<string>> GetEntitiesAsync(CancellationToken cancellationToken = default);
}
