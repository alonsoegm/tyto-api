using Tyto.Api.Application.Common;
using Tyto.Api.Application.Common.Errors;
using Tyto.Api.Application.Services;
using Tyto.Api.Domain.Enums;
using Tyto.Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Tyto.Api.Tests.Services;

public class AuditLogServiceTests
{
    private const string PerformedBy = "test-user";

    [Fact]
    public async Task Log_AddsEntryToContext()
    {
        using var db = TestDbContextFactory.Create();
        var service = new AuditLogService(db);
        var entityId = Guid.NewGuid();

        var result = service.Log(AuditAction.Create, AuditEntityType.LanguageModel, entityId, "GPT-4o", null, PerformedBy);
        await db.SaveChangesAsync(); // simulate the Unit of Work middleware commit

        result.IsSuccess.Should().BeTrue();
        var entry = await db.AuditLogs.SingleAsync();
        entry.Action.Should().Be(AuditAction.Create);
        entry.EntityType.Should().Be(AuditEntityType.LanguageModel);
        entry.EntityId.Should().Be(entityId);
        entry.PerformedBy.Should().Be(PerformedBy);
    }

    [Fact]
    public async Task GetAllAsync_FiltersByEntityType()
    {
        using var db = TestDbContextFactory.Create();
        var service = new AuditLogService(db);
        service.Log(AuditAction.Create, AuditEntityType.LanguageModel, Guid.NewGuid(), "lm", null, PerformedBy);
        service.Log(AuditAction.Create, AuditEntityType.MappedField, Guid.NewGuid(), "mf", null, PerformedBy);
        await db.SaveChangesAsync();

        var result = await service.GetAllAsync(new QueryParameters(), entityType: AuditEntityType.LanguageModel);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(1);
        result.Value.Items.Should().ContainSingle()
            .Which.EntityType.Should().Be(AuditEntityType.LanguageModel);
    }

    [Fact]
    public async Task GetAllAsync_FiltersByEntityId()
    {
        using var db = TestDbContextFactory.Create();
        var service = new AuditLogService(db);
        var targetId = Guid.NewGuid();
        service.Log(AuditAction.Update, AuditEntityType.Configuration, targetId, "cfg", null, PerformedBy);
        service.Log(AuditAction.Update, AuditEntityType.Configuration, Guid.NewGuid(), "cfg2", null, PerformedBy);
        await db.SaveChangesAsync();

        var result = await service.GetAllAsync(new QueryParameters(), entityId: targetId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle().Which.EntityId.Should().Be(targetId);
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotExists_ReturnsNotFound()
    {
        using var db = TestDbContextFactory.Create();
        var service = new AuditLogService(db);

        var result = await service.GetByIdAsync(Guid.NewGuid());

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Should().BeOfType<NotFoundError>();
    }
}
