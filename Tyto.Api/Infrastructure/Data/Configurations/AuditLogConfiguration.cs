using Tyto.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Tyto.Api.Infrastructure.Data.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.EntityName).HasMaxLength(200);
        builder.Property(x => x.PerformedBy).IsRequired().HasMaxLength(200);
        builder.Property(x => x.IpAddress).HasMaxLength(45);
        builder.Property(x => x.Changes).HasColumnType("text");

        builder.Property(x => x.Action).HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.EntityType).HasConversion<string>().HasMaxLength(50);

        // No FK constraints — AuditLog is polymorphic
        builder.HasIndex(x => new { x.EntityType, x.EntityId });
        builder.HasIndex(x => x.PerformedAt);
    }
}
