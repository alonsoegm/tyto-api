using FluentValidation;
using Tyto.Api.Application.DTOs.DatabaseConnection;
using Tyto.Api.Domain.Enums;

namespace Tyto.Api.Validators.DatabaseConnection;

/// <summary>
/// Validates <see cref="DatabaseConnectionCreateDto"/>.
/// Enforces that Salesforce or Dataverse fields are provided based on ConnectionType,
/// and that auth-specific fields are present for the chosen auth method.
/// </summary>
public class DatabaseConnectionCreateValidator : AbstractValidator<DatabaseConnectionCreateDto>
{
    public DatabaseConnectionCreateValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);

        // Salesforce rules
        When(x => x.ConnectionType == ConnectionType.Salesforce, () =>
        {
            RuleFor(x => x.SF_AuthMethod).NotNull().WithMessage("SF_AuthMethod is required for Salesforce connections.");
            RuleFor(x => x.SF_InstanceUrl).NotEmpty().MaximumLength(500).Must(BeAValidUrl).WithMessage("SF_InstanceUrl must be a valid URL.");

            When(x => x.SF_AuthMethod == SalesforceAuthMethod.ClientCredentials, () =>
            {
                RuleFor(x => x.SF_ConsumerKey).NotEmpty().WithMessage("SF_ConsumerKey is required for Client Credentials auth.");
                RuleFor(x => x.SF_ClientSecret).NotEmpty().WithMessage("SF_ClientSecret is required for Client Credentials auth.");
            });

            When(x => x.SF_AuthMethod == SalesforceAuthMethod.JwtBearer, () =>
            {
                RuleFor(x => x.SF_ConsumerKey).NotEmpty().WithMessage("SF_ConsumerKey is required for JWT Bearer auth.");
                RuleFor(x => x.SF_SigningKeySource).NotNull().WithMessage("SF_SigningKeySource is required for JWT Bearer auth.");

                When(x => x.SF_SigningKeySource == SigningKeySource.UploadPrivateKey, () =>
                {
                    RuleFor(x => x.SF_PrivateKeyFile).NotEmpty().WithMessage("SF_PrivateKeyFile is required for Upload Private Key source.");
                });

                When(x => x.SF_SigningKeySource == SigningKeySource.KeyVaultSecret, () =>
                {
                    RuleFor(x => x.SF_KeyVaultUrl).NotEmpty().WithMessage("SF_KeyVaultUrl is required for Key Vault Secret source.");
                    RuleFor(x => x.SF_KeyVaultSecretName).NotEmpty().WithMessage("SF_KeyVaultSecretName is required for Key Vault Secret source.");
                });
            });
        });

        // Dataverse rules
        When(x => x.ConnectionType == ConnectionType.MsDataverse, () =>
        {
            RuleFor(x => x.DV_AuthMethod).NotNull().WithMessage("DV_AuthMethod is required for Dataverse connections.");
            RuleFor(x => x.DV_EnvironmentUrl).NotEmpty().MaximumLength(500).Must(BeAValidUrl).WithMessage("DV_EnvironmentUrl must be a valid URL.");
            RuleFor(x => x.DV_TenantId).NotEmpty().WithMessage("DV_TenantId is required for Dataverse connections.");
            RuleFor(x => x.DV_ClientId).NotEmpty().WithMessage("DV_ClientId is required for Dataverse connections.");

            When(x => x.DV_AuthMethod == DataverseAuthMethod.ClientSecret, () =>
            {
                RuleFor(x => x.DV_ClientSecret).NotEmpty().WithMessage("DV_ClientSecret is required for Client Secret auth.");
            });

            When(x => x.DV_AuthMethod == DataverseAuthMethod.Certificate, () =>
            {
                RuleFor(x => x.DV_CertificateSource).NotNull().WithMessage("DV_CertificateSource is required for Certificate auth.");
                RuleFor(x => x.DV_CertificateData)
                    .NotEmpty().WithMessage("DV_CertificateData is required for Upload PFX certificate source.")
                    .When(x => x.DV_CertificateSource == CertificateSource.UploadPfx);
                RuleFor(x => x.DV_KeyVaultUrl)
                    .NotEmpty().WithMessage("DV_KeyVaultUrl is required for Key Vault certificate source.")
                    .When(x => x.DV_CertificateSource == CertificateSource.KeyVaultSecret);
                RuleFor(x => x.DV_KeyVaultCertificateName)
                    .NotEmpty().WithMessage("DV_KeyVaultCertificateName is required for Key Vault certificate source.")
                    .When(x => x.DV_CertificateSource == CertificateSource.KeyVaultSecret);
            });

            When(x => x.DV_AuthMethod == DataverseAuthMethod.ManagedIdentity, () =>
            {
                RuleFor(x => x.DV_ManagedIdentityType).NotNull().WithMessage("DV_ManagedIdentityType is required for Managed Identity auth.");
                RuleFor(x => x.DV_UserAssignedClientId)
                    .NotEmpty().WithMessage("DV_UserAssignedClientId is required for User Assigned managed identity.")
                    .When(x => x.DV_ManagedIdentityType == ManagedIdentityType.UserAssigned);
            });
        });
    }

    private static bool BeAValidUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
}
