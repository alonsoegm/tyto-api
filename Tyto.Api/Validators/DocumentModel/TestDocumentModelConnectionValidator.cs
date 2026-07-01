using FluentValidation;
using Tyto.Api.Application.DTOs.DocumentModel;

namespace Tyto.Api.Validators.DocumentModel;

/// <summary>Validates <see cref="TestDocumentModelConnectionDto"/> for pre-save connection checks.</summary>
public class TestDocumentModelConnectionValidator : AbstractValidator<TestDocumentModelConnectionDto>
{
    private static readonly string[] SupportedAuthMethods = ["ApiKey", "MicrosoftEntraId"];

    public TestDocumentModelConnectionValidator()
    {
        RuleFor(x => x.Endpoint)
            .NotEmpty()
            .MaximumLength(500)
            .Must(BeAValidUrl)
            .WithMessage("Endpoint must be a valid URL.");

        RuleFor(x => x.AuthMethod)
            .NotEmpty()
            .Must(method => SupportedAuthMethods.Contains(method, StringComparer.OrdinalIgnoreCase))
            .WithMessage("AuthMethod must be either 'ApiKey' or 'MicrosoftEntraId'.");

        RuleFor(x => x.ApiKey)
            .NotEmpty()
            .WithMessage("ApiKey is required when using ApiKey authentication.")
            .When(x => string.Equals(x.AuthMethod, "ApiKey", StringComparison.OrdinalIgnoreCase));

        RuleFor(x => x.ApiVersion)
            .MaximumLength(50)
            .When(x => !string.IsNullOrWhiteSpace(x.ApiVersion));

        RuleFor(x => x.ModelId)
            .MaximumLength(200)
            .When(x => !string.IsNullOrWhiteSpace(x.ModelId));
    }

    private static bool BeAValidUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
}
