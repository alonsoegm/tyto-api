using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tyto.Api.Domain.Entities;

namespace Tyto.Api.Infrastructure.Data.Configurations;

public class DatabaseConnectionConfiguration : IEntityTypeConfiguration<DatabaseConnection>
{
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

        // Salesforce
        builder.Property(x => x.SF_AuthMethod).HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.SF_InstanceUrl).HasMaxLength(500);
        builder.Property(x => x.SF_Username).HasMaxLength(300);
        builder.Property(x => x.SF_ConsumerKey).HasMaxLength(500);
        builder.Property(x => x.SF_ClientSecret).HasMaxLength(2000);
        builder.Property(x => x.SF_ApiVersion).HasMaxLength(50);
        builder.Property(x => x.SF_SigningKeySource).HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.SF_JwtAudience).HasMaxLength(500);
        builder.Property(x => x.SF_PrivateKeyFile).HasMaxLength(5000);
        builder.Property(x => x.SF_Passphrase).HasMaxLength(2000);
        builder.Property(x => x.SF_KeyVaultUrl).HasMaxLength(500);
        builder.Property(x => x.SF_KeyVaultSecretName).HasMaxLength(200);
        builder.Property(x => x.SF_IsSandbox).HasMaxLength(10);

        // Dataverse
        builder.Property(x => x.DV_AuthMethod).HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.DV_EnvironmentUrl).HasMaxLength(500);
        builder.Property(x => x.DV_TenantId).HasMaxLength(100);
        builder.Property(x => x.DV_ClientId).HasMaxLength(100);
        builder.Property(x => x.DV_ClientSecret).HasMaxLength(2000);
        builder.Property(x => x.DV_CertificateSource).HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.DV_CertificateData).HasMaxLength(10000);
        builder.Property(x => x.DV_CertificateThumbprint).HasMaxLength(200);
        builder.Property(x => x.DV_KeyVaultUrl).HasMaxLength(500);
        builder.Property(x => x.DV_KeyVaultCertificateName).HasMaxLength(200);
        builder.Property(x => x.DV_ManagedIdentityType).HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.DV_UserAssignedClientId).HasMaxLength(200);

        builder.HasIndex(x => x.Name).IsUnique();
    }
}
