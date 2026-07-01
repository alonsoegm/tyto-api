using FluentValidation;
using Tyto.Api.Application.DTOs.DatabaseConnection;
using Tyto.Api.Domain.Enums;

namespace Tyto.Api.Validators.DatabaseConnection;

/// <summary>
/// Validates <see cref="DatabaseConnectionUpdateDto"/>. Same auth-method rules as create,
/// but ConnectionType cannot be changed (omitted from UpdateDto).
/// </summary>
public class DatabaseConnectionUpdateValidator : AbstractValidator<DatabaseConnectionUpdateDto>
{
    public DatabaseConnectionUpdateValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);

        // Salesforce
        When(x => x.SF_AuthMethod.HasValue, () =>
        {
            RuleFor(x => x.SF_InstanceUrl).NotEmpty().MaximumLength(500).Must(BeAValidUrl).WithMessage("SF_InstanceUrl must be a valid URL.")
                .When(x => !string.IsNullOrWhiteSpace(x.SF_InstanceUrl));

            When(x => x.SF_AuthMethod == SalesforceAuthMethod.ClientCredentials, () =>
            {
                RuleFor(x => x.SF_ConsumerKey).NotEmpty().WithMessage("SF_ConsumerKey is required for Client Credentials auth.");
            });

            When(x => x.SF_AuthMethod == SalesforceAuthMethod.JwtBearer, () =>
            {
                RuleFor(x => x.SF_ConsumerKey).NotEmpty().WithMessage("SF_ConsumerKey is required for JWT Bearer auth.");
                RuleFor(x => x.SF_SigningKeySource).NotNull().WithMessage("SF_SigningKeySource is required for JWT Bearer auth.");

                When(x => x.SF_SigningKeySource == SigningKeySource.KeyVaultSecret, () =>
                {
                    RuleFor(x => x.SF_KeyVaultUrl).NotEmpty().WithMessage("SF_KeyVaultUrl is required for Key Vault Secret source.");
                    RuleFor(x => x.SF_KeyVaultSecretName).NotEmpty().WithMessage("SF_KeyVaultSecretName is required for Key Vault Secret source.");
                });
            });
        });

        // Dataverse
        When(x => x.DV_AuthMethod.HasValue, () =>
        {
            RuleFor(x => x.DV_EnvironmentUrl).NotEmpty().MaximumLength(500).Must(BeAValidUrl).WithMessage("DV_EnvironmentUrl must be a valid URL.")
                .When(x => !string.IsNullOrWhiteSpace(x.DV_EnvironmentUrl));

            When(x => x.DV_AuthMethod == DataverseAuthMethod.Certificate, () =>
            {
                RuleFor(x => x.DV_CertificateSource).NotNull().WithMessage("DV_CertificateSource is required for Certificate auth.");
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
