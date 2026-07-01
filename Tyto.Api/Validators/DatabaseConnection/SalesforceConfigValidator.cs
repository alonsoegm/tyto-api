using FluentValidation;
using Tyto.Api.Domain.Configs;
using Tyto.Api.Domain.Enums;

namespace Tyto.Api.Validators.DatabaseConnection;

/// <summary>
/// Validates the Salesforce <see cref="SalesforceConfig"/> payload carried in a connection's Config.
/// Secret fields sent as the masked placeholder on update still satisfy NotEmpty, preserving the
/// stored value.
/// </summary>
public class SalesforceConfigValidator : AbstractValidator<SalesforceConfig>
{
    public SalesforceConfigValidator()
    {
        RuleFor(x => x.AuthMethod).NotNull().WithMessage("Config.authMethod is required for Salesforce connections.");
        RuleFor(x => x.InstanceUrl).NotEmpty().MaximumLength(500).Must(BeAValidUrl).WithMessage("Config.instanceUrl must be a valid URL.");

        When(x => x.AuthMethod == SalesforceAuthMethod.ClientCredentials, () =>
        {
            RuleFor(x => x.ConsumerKey).NotEmpty().WithMessage("Config.consumerKey is required for Client Credentials auth.");
            RuleFor(x => x.ClientSecret).NotEmpty().WithMessage("Config.clientSecret is required for Client Credentials auth.");
        });

        When(x => x.AuthMethod == SalesforceAuthMethod.JwtBearer, () =>
        {
            RuleFor(x => x.ConsumerKey).NotEmpty().WithMessage("Config.consumerKey is required for JWT Bearer auth.");
            RuleFor(x => x.SigningKeySource).NotNull().WithMessage("Config.signingKeySource is required for JWT Bearer auth.");

            When(x => x.SigningKeySource == Domain.Enums.SigningKeySource.UploadPrivateKey, () =>
            {
                RuleFor(x => x.PrivateKeyFile).NotEmpty().WithMessage("Config.privateKeyFile is required for Upload Private Key source.");
            });

            When(x => x.SigningKeySource == Domain.Enums.SigningKeySource.KeyVaultSecret, () =>
            {
                RuleFor(x => x.KeyVaultUrl).NotEmpty().WithMessage("Config.keyVaultUrl is required for Key Vault Secret source.");
                RuleFor(x => x.KeyVaultSecretName).NotEmpty().WithMessage("Config.keyVaultSecretName is required for Key Vault Secret source.");
            });
        });
    }

    private static bool BeAValidUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
}
