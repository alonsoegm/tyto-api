using FluentAssertions;
using FluentResults;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Tyto.Api.Application.DTOs.Configuration;
using Tyto.Api.Application.Interfaces;
using Tyto.Api.Application.Services;
using Tyto.Api.Application.Services.Extraction.Sinks;
using Tyto.Api.Domain.Entities;
using Tyto.Api.Domain.Enums;
using Tyto.Api.Infrastructure.Data;
using Tyto.Api.Tests.Infrastructure;
using Tyto.Api.Validators.Configuration;

namespace Tyto.Api.Tests.Services;

/// <summary>
/// Covers US7.1: TargetObject is resolved from the connection type (never its name). Internal
/// connections don't require a target and resolve to <c>ExtractionResults</c> server-side; external
/// connections still require a non-empty target.
/// </summary>
public class ConfigurationServiceTargetObjectTests
{
    private static readonly Guid LanguageModelId = Guid.NewGuid();
    private static readonly Guid InternalConnectionId = Guid.NewGuid();
    private static readonly Guid ExternalConnectionId = Guid.NewGuid();

    [Fact]
    public async Task CreateAsync_InternalConnection_AllowsEmptyTarget_AndResolvesToExtractionResults()
    {
        var (service, _) = CreateService(out var db);
        await SeedAsync(db);

        var dto = CreateDto(InternalConnectionId, targetObject: "");

        var result = await service.CreateAsync(dto, "tester");

        result.IsSuccess.Should().BeTrue();
        result.Value.TargetObject.Should().Be(InternalSqlSink.EntityName);
        result.Value.TargetObject.Should().Be("ExtractionResults");
    }

    [Fact]
    public async Task CreateAsync_ExternalConnection_WithoutTarget_Fails()
    {
        var (service, _) = CreateService(out var db);
        await SeedAsync(db);

        var dto = CreateDto(ExternalConnectionId, targetObject: "");

        var result = await service.CreateAsync(dto, "tester");

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Message.Contains("Target object is required"));
    }

    [Fact]
    public async Task CreateAsync_ExternalConnection_WithTarget_Succeeds()
    {
        var (service, _) = CreateService(out var db);
        await SeedAsync(db);

        var dto = CreateDto(ExternalConnectionId, targetObject: "Account");

        var result = await service.CreateAsync(dto, "tester");

        result.IsSuccess.Should().BeTrue();
        result.Value.TargetObject.Should().Be("Account");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static (ConfigurationService Service, Mock<IAuditLogService> Audit) CreateService(out TytoDbContext db)
    {
        db = TestDbContextFactory.Create();

        var audit = new Mock<IAuditLogService>();
        audit
            .Setup(a => a.Log(It.IsAny<AuditAction>(), It.IsAny<AuditEntityType>(), It.IsAny<Guid>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string?>()))
            .Returns(Result.Ok());

        var service = new ConfigurationService(
            db,
            audit.Object,
            new ConfigurationCreateValidator(),
            new ConfigurationUpdateValidator(),
            NullLogger<ConfigurationService>.Instance);

        return (service, audit);
    }

    private static async Task SeedAsync(TytoDbContext db)
    {
        db.LanguageModels.Add(new LanguageModel { Id = LanguageModelId, Name = "gpt-4o" });
        db.DatabaseConnections.Add(new DatabaseConnection
        {
            Id = InternalConnectionId,
            Name = "Tyto Internal",
            ConnectionType = ConnectionType.InternalSql,
            IsInternal = true,
        });
        db.DatabaseConnections.Add(new DatabaseConnection
        {
            Id = ExternalConnectionId,
            Name = "Prod Salesforce",
            ConnectionType = ConnectionType.Salesforce,
            IsInternal = false,
        });
        await db.SaveChangesAsync();
    }

    private static ConfigurationCreateDto CreateDto(Guid connectionId, string targetObject) => new(
        Name: $"Config {Guid.NewGuid():N}",
        Description: "",
        ExtractionStrategy: ExtractionStrategy.SingleModel,
        ModelSelectionMode: ModelSelectionMode.Fixed,
        LanguageModelId: LanguageModelId,
        DocumentModelId: null,
        DatabaseConnectionId: connectionId,
        TargetObject: targetObject,
        SystemPrompt: "test",
        UserPromptTemplate: null,
        MaxTokens: 4096,
        Temperature: 0,
        MaxUploadSizeMB: 25,
        AcceptedFileTypes: new List<string> { "pdf" },
        MappedFields: new List<MappedFieldInlineDto>
        {
            new("clientName", "Client Name", FieldType.Text, RequirementLevel.Required, null, null),
        });
}
