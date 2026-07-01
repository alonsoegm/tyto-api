using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Tyto.Api.Application.Common.Errors;
using Tyto.Api.Application.Interfaces;
using Tyto.Api.Domain.Entities;
using Tyto.Api.Domain.Enums;

namespace Tyto.Api.Application.Services.Extraction;

/// <summary>
/// Resolves the extraction sink for a connection type from the keyed DI registrations. Cosmos DB is a
/// recognized-but-not-implemented type for the MVP; any other type without a registered sink is a
/// controlled failure.
/// </summary>
public class ExtractionSinkResolver : IExtractionSinkResolver
{
    private readonly IServiceProvider _serviceProvider;

    public ExtractionSinkResolver(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

    public Result<IExtractionSink> Resolve(DatabaseConnection connection) => Resolve(connection.ConnectionType);

    public Result<IExtractionSink> Resolve(ConnectionType connectionType)
    {
        if (connectionType == ConnectionType.CosmosDb)
            return Result.Fail<IExtractionSink>(
                new ValidationError("Azure Cosmos DB is coming soon; its extraction sink is not implemented yet."));

        var sink = _serviceProvider.GetKeyedService<IExtractionSink>(connectionType.ToString());
        if (sink is null)
            return Result.Fail<IExtractionSink>(
                new InternalError($"No extraction sink is registered for connection type '{connectionType}'."));

        return Result.Ok(sink);
    }
}
