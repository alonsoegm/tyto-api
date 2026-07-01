using Tyto.Api.Domain.Entities;
using Tyto.Api.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Tyto.Api.Infrastructure.Data.Configurations;

public class LanguageModelConfiguration : IEntityTypeConfiguration<LanguageModel>
{
    public void Configure(EntityTypeBuilder<LanguageModel> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.Endpoint).IsRequired().HasMaxLength(500);
        builder.Property(x => x.DeploymentName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.ApiVersion).HasMaxLength(50);
        builder.Property(x => x.ApiKeyEncrypted).HasMaxLength(2000);
        builder.Property(x => x.UserAssignedClientId).HasMaxLength(200);
        builder.Property(x => x.CreatedBy).HasMaxLength(200);
        builder.Property(x => x.UpdatedBy).HasMaxLength(200);

        // New fields for provider-specific config and test results
        builder.Property(x => x.ModelName).HasMaxLength(255);
        builder.Property(x => x.ApiSurface).HasMaxLength(50);
        builder.Property(x => x.LastTestStatus).HasMaxLength(50);
        builder.Property(x => x.LastTestMessage).HasMaxLength(1000);

        builder.Property(x => x.ServiceType)
            .HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.AuthenticationMethod)
            .HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.ManagedIdentityType)
            .HasConversion<string>().HasMaxLength(50);

        builder.Property(x => x.IsDefault).HasDefaultValue(false);

        builder.HasIndex(x => x.Name).IsUnique();

        // Guarantees at most one default language model at the database level.
        builder.HasIndex(x => x.IsDefault)
            .IsUnique()
            .HasFilter("[IsDefault] = 1");
    }
}
