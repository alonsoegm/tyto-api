using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tyto.Api.Application.Common.Errors;
using Tyto.Api.Application.Interfaces;
using Tyto.Api.Application.Services.Extraction;
using Tyto.Api.Application.Services.Extraction.Sinks;
using Tyto.Api.Domain.Enums;
using Tyto.Api.Infrastructure.Data;

namespace Tyto.Api.Tests.Services;

/// <summary>
/// Verifies sink resolution over the real keyed-DI registrations: each supported type resolves to its
/// sink, Cosmos DB is a controlled not-implemented result, unknown types fail, and sinks are scoped.
/// </summary>
public class ExtractionSinkResolverTests
{
    [Theory]
    [InlineData(ConnectionType.InternalSql, typeof(InternalSqlSink))]
    [InlineData(ConnectionType.Salesforce, typeof(SalesforceSink))]
    [InlineData(ConnectionType.Dataverse, typeof(DataverseSink))]
    public void Resolve_ReturnsSinkMatchingConnectionType(ConnectionType type, Type expectedSink)
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<IExtractionSinkResolver>();

        var result = resolver.Resolve(type);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeOfType(expectedSink);
        result.Value.ConnectionType.Should().Be(type.ToString());
    }

    [Fact]
    public void Resolve_CosmosDb_ReturnsControlledNotImplementedResult()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<IExtractionSinkResolver>();

        var result = resolver.Resolve(ConnectionType.CosmosDb);

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Should().BeOfType<ValidationError>();
    }

    [Fact]
    public void Resolve_UnknownConnectionType_Fails()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<IExtractionSinkResolver>();

        var result = resolver.Resolve((ConnectionType)999);

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Should().BeOfType<InternalError>();
    }

    [Fact]
    public void Resolve_ReturnsSameScopedInstanceWithinAScope_AndDistinctAcrossScopes()
    {
        using var provider = BuildProvider();

        IExtractionSink first, second, other;
        using (var scope = provider.CreateScope())
        {
            var resolver = scope.ServiceProvider.GetRequiredService<IExtractionSinkResolver>();
            first = resolver.Resolve(ConnectionType.InternalSql).Value;
            second = resolver.Resolve(ConnectionType.InternalSql).Value;
        }
        using (var scope = provider.CreateScope())
        {
            var resolver = scope.ServiceProvider.GetRequiredService<IExtractionSinkResolver>();
            other = resolver.Resolve(ConnectionType.InternalSql).Value;
        }

        first.Should().BeSameAs(second, "one scoped sink instance is reused within a request scope");
        first.Should().NotBeSameAs(other, "a different scope gets its own sink instance");
    }

    private static ServiceProvider BuildProvider() =>
        new ServiceCollection()
            .AddLogging()
            .AddDbContext<TytoDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()))
            .AddScoped<IExtractionSinkResolver, ExtractionSinkResolver>()
            .AddKeyedScoped<IExtractionSink, InternalSqlSink>(ConnectionType.InternalSql.ToString())
            .AddKeyedScoped<IExtractionSink, SalesforceSink>(ConnectionType.Salesforce.ToString())
            .AddKeyedScoped<IExtractionSink, DataverseSink>(ConnectionType.Dataverse.ToString())
            .BuildServiceProvider(validateScopes: true);
}
