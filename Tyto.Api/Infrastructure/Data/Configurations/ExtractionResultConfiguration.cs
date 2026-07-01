using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tyto.Api.Domain.Entities;

namespace Tyto.Api.Infrastructure.Data.Configurations;

public class ExtractionResultConfiguration : IEntityTypeConfiguration<ExtractionResult>
{
    public void Configure(EntityTypeBuilder<ExtractionResult> builder)
    {
        builder.HasKey(x => x.Id);

        // Structured output stored as JSON (NVARCHAR(MAX) — Azure SQL has no native JSON type here).
        builder.Property(x => x.ExtractedData).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(x => x.LanguageModelName).HasMaxLength(200);
        builder.Property(x => x.DocumentModelName).HasMaxLength(200);
        builder.Property(x => x.CreatedBy).HasMaxLength(200);
        builder.Property(x => x.UpdatedBy).HasMaxLength(200);

        // Deleting a run removes its extraction results. The configuration link is Restrict to avoid
        // multiple cascade paths (Configuration -> RunHistory -> ExtractionResult already cascades).
        builder.HasOne(x => x.RunHistory)
            .WithMany()
            .HasForeignKey(x => x.RunHistoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Configuration)
            .WithMany()
            .HasForeignKey(x => x.ConfigurationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
