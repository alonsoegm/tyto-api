using FluentValidation;
using Tyto.Api.Domain.Configs;
using Tyto.Api.Domain.Enums;

namespace Tyto.Api.Validators.DatabaseConnection;

/// <summary>
/// Validates the Dataverse <see cref="DataverseConfig"/> payload carried in a connection's Config.
/// Secret fields sent as the masked placeholder on update still satisfy NotEmpty, preserving the
/// stored value.
/// </summary>
public class DataverseConfigValidator : AbstractValidator<DataverseConfig>
{
    public DataverseConfigValidator()
    {
        RuleFor(x => x.AuthMethod).NotNull().WithMessage("Config.authMethod is required for Dataverse connections.");
        RuleFor(x => x.EnvironmentUrl).NotEmpty().MaximumLength(500).Must(BeAValidUrl).WithMessage("Config.environmentUrl must be a valid URL.");
        RuleFor(x => x.TenantId).NotEmpty().WithMessage("Config.tenantId is required for Dataverse connections.");
        RuleFor(x => x.ClientId).NotEmpty().WithMessage("Config.clientId is required for Dataverse connections.");

        When(x => x.AuthMethod == DataverseAuthMethod.ClientSecret, () =>
        {
            RuleFor(x => x.ClientSecret).NotEmpty().WithMessage("Config.clientSecret is required for Client Secret auth.");
        });

        When(x => x.AuthMethod == DataverseAuthMethod.Certificate, () =>
        {
            RuleFor(x => x.CertificateSource).NotNull().WithMessage("Config.certificateSource is required for Certificate auth.");
            RuleFor(x => x.CertificateData)
                .NotEmpty().WithMessage("Config.certificateData is required for Upload PFX certificate source.")
                .When(x => x.CertificateSource == Domain.Enums.CertificateSource.UploadPfx);
            RuleFor(x => x.KeyVaultUrl)
                .NotEmpty().WithMessage("Config.keyVaultUrl is required for Key Vault certificate source.")
                .When(x => x.CertificateSource == Domain.Enums.CertificateSource.KeyVaultSecret);
            RuleFor(x => x.KeyVaultCertificateName)
                .NotEmpty().WithMessage("Config.keyVaultCertificateName is required for Key Vault certificate source.")
                .When(x => x.CertificateSource == Domain.Enums.CertificateSource.KeyVaultSecret);
        });

        When(x => x.AuthMethod == DataverseAuthMethod.ManagedIdentity, () =>
        {
            RuleFor(x => x.ManagedIdentityType).NotNull().WithMessage("Config.managedIdentityType is required for Managed Identity auth.");
            RuleFor(x => x.UserAssignedClientId)
                .NotEmpty().WithMessage("Config.userAssignedClientId is required for User Assigned managed identity.")
                .When(x => x.ManagedIdentityType == Domain.Enums.ManagedIdentityType.UserAssigned);
        });
    }

    private static bool BeAValidUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
}
