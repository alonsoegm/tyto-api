using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tyto.Api.Domain.Entities;

namespace Tyto.Api.Infrastructure.Data.Configurations;

public class RunHistoryConfiguration : IEntityTypeConfiguration<RunHistory>
{
    public void Configure(EntityTypeBuilder<RunHistory> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ErrorMessage).HasMaxLength(2000);
        builder.Property(x => x.TriggeredBy).IsRequired().HasMaxLength(200);
        builder.Property(x => x.CreatedBy).HasMaxLength(200);
        builder.Property(x => x.UpdatedBy).HasMaxLength(200);

        // RawInput/RawOutput are large text — no max length constraint
        builder.Property(x => x.RawInput).HasColumnType("text");
        builder.Property(x => x.RawOutput).HasColumnType("text");

        builder.HasOne(x => x.Configuration)
            .WithMany(x => x.RunHistories)
            .HasForeignKey(x => x.ConfigurationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
