using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Tyto.Api.Application.DTOs.Extraction;
using Tyto.Api.Application.Services.Extraction.Sinks;
using Tyto.Api.Domain.Enums;
using Tyto.Api.Tests.Infrastructure;

namespace Tyto.Api.Tests.Services;

/// <summary>
/// Covers the Internal SQL sink: the fixed entity, persistence into ExtractionResults, provenance
/// preservation, cancellation, and that it defers commit to the ambient Unit of Work.
/// </summary>
public class InternalSqlSinkTests
{
    [Fact]
    public async Task GetEntitiesAsync_ReturnsSingleExtractionResultsEntity()
    {
        using var db = TestDbContextFactory.Create();
        var sink = new InternalSqlSink(db, NullLogger<InternalSqlSink>.Instance);

        var entities = await sink.GetEntitiesAsync();

        entities.Should().ContainSingle().Which.Should().Be("ExtractionResults");
        sink.ConnectionType.Should().Be(ConnectionType.InternalSql.ToString());
    }

    [Fact]
    public async Task WriteAsync_PersistsResult_PreservingProvenance()
    {
        using var db = TestDbContextFactory.Create();
        var sink = new InternalSqlSink(db, NullLogger<InternalSqlSink>.Instance);
        var result = SampleResult();

        await sink.WriteAsync(result);
        await db.SaveChangesAsync();

        var stored = await db.ExtractionResults.SingleAsync();
        stored.ConfigurationId.Should().Be(result.ConfigurationId);
        stored.RunHistoryId.Should().Be(result.RunHistoryId);
        stored.LanguageModelName.Should().Be("gpt-4o");
        stored.CreatedBy.Should().Be("system");
        stored.CreatedAt.Should().BeAfter(default);

        // Field-level confidence/source metadata inside the JSON survives the round-trip.
        var fields = JsonNode.Parse(stored.ExtractedData)!.AsObject();
        fields["invoiceNumber"]!["value"]!.GetValue<string>().Should().Be("INV-1");
        fields["invoiceNumber"]!["confidence"]!.GetValue<double>().Should().Be(0.98);
    }

    [Fact]
    public async Task WriteAsync_DoesNotCommitIndependentlyOfUnitOfWork()
    {
        using var db = TestDbContextFactory.Create();
        var sink = new InternalSqlSink(db, NullLogger<InternalSqlSink>.Instance);

        await sink.WriteAsync(SampleResult());

        // The row is staged (Added) but not committed — the request-scoped UoW middleware commits.
        var entry = db.ChangeTracker.Entries<Tyto.Api.Domain.Entities.ExtractionResult>().Should().ContainSingle().Which;
        entry.State.Should().Be(EntityState.Added);
    }

    [Fact]
    public async Task WriteAsync_WhenCancelled_Throws()
    {
        using var db = TestDbContextFactory.Create();
        var sink = new InternalSqlSink(db, NullLogger<InternalSqlSink>.Instance);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => sink.WriteAsync(SampleResult(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static ExtractionResult SampleResult()
    {
        var fields = new JsonObject
        {
            ["invoiceNumber"] = new JsonObject
            {
                ["value"] = "INV-1",
                ["confidence"] = 0.98,
                ["source"] = "page-1"
            }
        };
        return new ExtractionResult(
            ConfigurationId: Guid.NewGuid(),
            RunHistoryId: Guid.NewGuid(),
            Fields: fields,
            LanguageModelName: "gpt-4o",
            DocumentModelName: null,
            DurationMs: 1234);
    }
}
