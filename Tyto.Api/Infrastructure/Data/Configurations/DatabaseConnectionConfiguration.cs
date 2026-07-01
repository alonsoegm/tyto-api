using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tyto.Api.Domain.Entities;
using Tyto.Api.Domain.Enums;

namespace Tyto.Api.Infrastructure.Data.Configurations;

public class DatabaseConnectionConfiguration : IEntityTypeConfiguration<DatabaseConnection>
{
    // Fixed timestamp for the deterministic internal seed. HasData must not use DateTime.UtcNow,
    // which would make the model snapshot non-deterministic and produce spurious migrations.
    private static readonly DateTime SeedTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<DatabaseConnection> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.LastTestStatus).HasMaxLength(50);
        builder.Property(x => x.LastTestMessage).HasMaxLength(1000);
        builder.Property(x => x.CreatedBy).HasMaxLength(200);
        builder.Property(x => x.UpdatedBy).HasMaxLength(200);

        builder.Property(x => x.ConnectionType).HasConversion<string>().HasMaxLength(50);

        builder.Property(x => x.IsInternal).HasDefaultValue(false);

        // Provider-agnostic JSON payload. Azure SQL has no native JSON type on this EF Core version,
        // so it maps to NVARCHAR(MAX) while the domain contract stays "a JSON string".
        builder.Property(x => x.Config).HasColumnType("nvarchar(max)");

        builder.HasIndex(x => x.Name).IsUnique();

        SeedInternalConnection(builder);
    }

    private static void SeedInternalConnection(EntityTypeBuilder<DatabaseConnection> builder) =>
        builder.HasData(new DatabaseConnection
        {
            Id = DatabaseConnection.InternalConnectionId,
            Name = DatabaseConnection.InternalConnectionName,
            Description = "System-managed Azure SQL destination. Extracted data is stored automatically.",
            ConnectionType = ConnectionType.InternalSql,
            IsInternal = true,
            Config = null,
            CreatedAt = SeedTimestamp,
            UpdatedAt = SeedTimestamp,
            CreatedBy = "system",
            UpdatedBy = "system"
        });
}
