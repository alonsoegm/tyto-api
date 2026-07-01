using FluentResults;
using Tyto.Api.Domain.Entities;
using Tyto.Api.Domain.Enums;

namespace Tyto.Api.Application.Interfaces;

/// <summary>
/// Resolves the <see cref="IExtractionSink"/> for a connection or connection type. Centralizes sink
/// selection so callers never resolve sinks from the service provider directly. Unsupported types
/// produce a controlled failure that flows through the standard result/ProblemDetails pipeline.
/// </summary>
public interface IExtractionSinkResolver
{
    /// <summary>Resolves the sink for the given connection's type.</summary>
    Result<IExtractionSink> Resolve(DatabaseConnection connection);

    /// <summary>Resolves the sink for the given connection type.</summary>
    Result<IExtractionSink> Resolve(ConnectionType connectionType);
}
