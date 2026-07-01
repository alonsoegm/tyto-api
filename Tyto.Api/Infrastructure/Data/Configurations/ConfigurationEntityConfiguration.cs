using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tyto.Api.Domain.Entities;

namespace Tyto.Api.Infrastructure.Data.Configurations;

public class ConfigurationEntityConfiguration : IEntityTypeConfiguration<Configuration>
{
    public void Configure(EntityTypeBuilder<Configuration> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.TargetObject).IsRequired().HasMaxLength(200);
        builder.Property(x => x.SystemPrompt).HasMaxLength(5000);
        builder.Property(x => x.UserPromptTemplate).HasMaxLength(5000);
        builder.Property(x => x.MaxUploadSizeMB).IsRequired().HasDefaultValue(25);
        builder.Property(x => x.AcceptedFileTypes).HasMaxLength(500);
        builder.Property(x => x.CreatedBy).HasMaxLength(200);
        builder.Property(x => x.UpdatedBy).HasMaxLength(200);

        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.ExtractionStrategy).HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.ModelSelectionMode).HasConversion<string>().HasMaxLength(50);

        builder.HasOne(x => x.LanguageModel)
            .WithMany(x => x.Configurations)
            .HasForeignKey(x => x.LanguageModelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.DocumentModel)
            .WithMany(x => x.Configurations)
            .HasForeignKey(x => x.DocumentModelId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.DatabaseConnection)
            .WithMany(x => x.Configurations)
            .HasForeignKey(x => x.DatabaseConnectionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.Name).IsUnique();
    }
}
