using FluentAssertions;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Tyto.Api.Application.Common;
using Tyto.Api.Application.Common.Errors;
using Tyto.Api.Application.DTOs.MappedField;
using Tyto.Api.Application.Interfaces;
using Tyto.Api.Application.Services;
using Tyto.Api.Domain.Entities;
using Tyto.Api.Domain.Enums;
using Tyto.Api.Infrastructure.Data;
using Tyto.Api.Tests.Infrastructure;
using Tyto.Api.Validators.MappedField;

namespace Tyto.Api.Tests.Services;

public class MappedFieldServiceTests
{
    private const string PerformedBy = "test-user";

    private readonly Mock<IAuditLogService> _auditLog = new();

    public MappedFieldServiceTests()
    {
        _auditLog
            .Setup(x => x.Log(
                It.IsAny<AuditAction>(),
                It.IsAny<AuditEntityType>(),
                It.IsAny<Guid>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string?>()))
            .Returns(Result.Ok());
    }

    private MappedFieldService CreateService(TytoDbContext db) =>
        new(db, _auditLog.Object, new MappedFieldCreateValidator(), new MappedFieldUpdateValidator(),
            NullLogger<MappedFieldService>.Instance);

    private static MappedFieldCreateDto ValidCreateDto(Guid configurationId) =>
        new(configurationId, "InvoiceNumber", "Invoice Number", FieldType.Text,
            RequirementLevel.Required, null, null, 0, null);

    private static async Task<Configuration> SeedConfigurationAsync(TytoDbContext db)
    {
        var configuration = new Configuration { Name = "Test Config" };
        db.Configurations.Add(configuration);
        await db.SaveChangesAsync();
        return configuration;
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotExists_ReturnsNotFound()
    {
        using var db = TestDbContextFactory.Create();
        var service = CreateService(db);

        var result = await service.GetByIdAsync(Guid.NewGuid());

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Should().BeOfType<NotFoundError>();
    }

    [Fact]
    public async Task GetByIdAsync_WhenExists_ReturnsDto()
    {
        using var db = TestDbContextFactory.Create();
        var config = await SeedConfigurationAsync(db);
        var field = new MappedField { ConfigurationId = config.Id, FieldName = "Total", DisplayLabel = "Total" };
        db.MappedFields.Add(field);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.GetByIdAsync(field.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(field.Id);
        result.Value.FieldName.Should().Be("Total");
    }

    [Fact]
    public async Task GetAllAsync_FiltersByConfiguration_AndOrdersBySortOrder()
    {
        using var db = TestDbContextFactory.Create();
        var configA = await SeedConfigurationAsync(db);
        var configB = await SeedConfigurationAsync(db);

        db.MappedFields.AddRange(
            new MappedField { ConfigurationId = configA.Id, FieldName = "B", DisplayLabel = "B", SortOrder = 2 },
            new MappedField { ConfigurationId = configA.Id, FieldName = "A", DisplayLabel = "A", SortOrder = 1 },
            new MappedField { ConfigurationId = configB.Id, FieldName = "Other", DisplayLabel = "Other", SortOrder = 1 });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.GetAllAsync(configA.Id, new QueryParameters());

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(2);
        result.Value.Items.Select(x => x.FieldName).Should().ContainInOrder("A", "B");
    }

    [Fact]
    public async Task CreateAsync_WithInvalidDto_ReturnsValidationError()
    {
        using var db = TestDbContextFactory.Create();
        var service = CreateService(db);
        var dto = new MappedFieldCreateDto(Guid.NewGuid(), "", "", FieldType.Text,
            RequirementLevel.Optional, null, null, -1, null);

        var result = await service.CreateAsync(dto, PerformedBy);

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Should().BeOfType<ValidationError>();
    }

    [Fact]
    public async Task CreateAsync_WhenConfigurationMissing_ReturnsNotFound()
    {
        using var db = TestDbContextFactory.Create();
        var service = CreateService(db);

        var result = await service.CreateAsync(ValidCreateDto(Guid.NewGuid()), PerformedBy);

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Should().BeOfType<NotFoundError>();
    }

    [Fact]
    public async Task CreateAsync_WhenValid_AddsEntity_SetsAudit_AndLogs()
    {
        using var db = TestDbContextFactory.Create();
        var config = await SeedConfigurationAsync(db);
        var service = CreateService(db);

        var result = await service.CreateAsync(ValidCreateDto(config.Id), PerformedBy);
        await db.SaveChangesAsync(); // simulate the Unit of Work middleware commit

        result.IsSuccess.Should().BeTrue();
        result.Value.CreatedBy.Should().Be(PerformedBy);
        (await db.MappedFields.CountAsync(x => x.ConfigurationId == config.Id)).Should().Be(1);
        _auditLog.Verify(x => x.Log(AuditAction.Create, AuditEntityType.MappedField,
            It.IsAny<Guid>(), "InvoiceNumber", null, PerformedBy, null), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WhenNotExists_ReturnsNotFound()
    {
        using var db = TestDbContextFactory.Create();
        var service = CreateService(db);
        var dto = new MappedFieldUpdateDto("Name", "Label", FieldType.Text,
            RequirementLevel.Optional, null, null, 0, null);

        var result = await service.UpdateAsync(Guid.NewGuid(), dto, PerformedBy);

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Should().BeOfType<NotFoundError>();
    }

    [Fact]
    public async Task UpdateAsync_WhenParentIsSelf_ReturnsConflict()
    {
        using var db = TestDbContextFactory.Create();
        var config = await SeedConfigurationAsync(db);
        var field = new MappedField { ConfigurationId = config.Id, FieldName = "Total", DisplayLabel = "Total" };
        db.MappedFields.Add(field);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var dto = new MappedFieldUpdateDto("Total", "Total", FieldType.Text,
            RequirementLevel.Optional, null, null, 0, field.Id);

        var result = await service.UpdateAsync(field.Id, dto, PerformedBy);

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Should().BeOfType<ConflictError>();
    }

    [Fact]
    public async Task DeleteAsync_WhenExists_RemovesEntity()
    {
        using var db = TestDbContextFactory.Create();
        var config = await SeedConfigurationAsync(db);
        var field = new MappedField { ConfigurationId = config.Id, FieldName = "Total", DisplayLabel = "Total" };
        db.MappedFields.Add(field);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.DeleteAsync(field.Id, PerformedBy);
        await db.SaveChangesAsync(); // simulate the Unit of Work middleware commit

        result.IsSuccess.Should().BeTrue();
        (await db.MappedFields.CountAsync()).Should().Be(0);
        _auditLog.Verify(x => x.Log(AuditAction.Delete, AuditEntityType.MappedField,
            field.Id, "Total", null, PerformedBy, null), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WhenNotExists_ReturnsNotFound()
    {
        using var db = TestDbContextFactory.Create();
        var service = CreateService(db);

        var result = await service.DeleteAsync(Guid.NewGuid(), PerformedBy);

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Should().BeOfType<NotFoundError>();
    }
}
