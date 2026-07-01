using FluentValidation;
using Tyto.Api.Application.DTOs.DocumentModel;
using Tyto.Api.Domain.Enums;

namespace Tyto.Api.Validators.DocumentModel;

/// <summary>Validates <see cref="DocumentModelCreateDto"/>. Ensures endpoint, model ID, and auth fields are valid.</summary>
public class DocumentModelCreateValidator : AbstractValidator<DocumentModelCreateDto>
{
    public DocumentModelCreateValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.Endpoint).NotEmpty().MaximumLength(500).Must(BeAValidUrl).WithMessage("Endpoint must be a valid URL.");
        RuleFor(x => x.ModelId).MaximumLength(200);
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
