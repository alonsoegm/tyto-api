using Tyto.Api.Application.DTOs.LanguageModel;
using Tyto.Api.Domain.Enums;
using FluentValidation;
using ManagedIdentityType = Tyto.Api.Domain.Enums.ManagedIdentityType;

namespace Tyto.Api.Validators.LanguageModel;

/// <summary>Validates <see cref="LanguageModelCreateDto"/>. Ensures required fields are present and API key is provided when using ApiKey auth.</summary>
public class LanguageModelCreateValidator : AbstractValidator<LanguageModelCreateDto>
{
    public LanguageModelCreateValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.Endpoint).NotEmpty().MaximumLength(500).Must(BeAValidUrl).WithMessage("Endpoint must be a valid URL.");
        RuleFor(x => x.DeploymentName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ApiVersion).MaximumLength(50);

        RuleFor(x => x.ApiKey)
            .NotEmpty().WithMessage("ApiKey is required when using ApiKey authentication.")
            .When(x => x.AuthenticationMethod == AuthenticationMethod.ApiKey);

        RuleFor(x => x.UserAssignedClientId)
            .NotEmpty().WithMessage("UserAssignedClientId is required when using a User Assigned managed identity.")
            .When(x => x.AuthenticationMethod == AuthenticationMethod.ManagedIdentity && x.ManagedIdentityType == ManagedIdentityType.UserAssigned);
    }

    private static bool BeAValidUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
}
