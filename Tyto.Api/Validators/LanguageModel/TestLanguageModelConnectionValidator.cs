using Tyto.Api.Application.DTOs.LanguageModel;
using FluentValidation;

namespace Tyto.Api.Validators.LanguageModel;

/// <summary>
/// Validates <see cref="TestLanguageModelConnectionDto"/>.
/// Ensures required fields are present based on service type and authentication method.
/// </summary>
public class TestLanguageModelConnectionValidator : AbstractValidator<TestLanguageModelConnectionDto>
{
    private static readonly string[] ValidServiceTypes = { "AzureOpenAI", "AzureFoundry" };
    private static readonly string[] ValidAuthMethods = { "ApiKey", "MicrosoftEntraId" };
    private static readonly string[] ValidApiSurfaces = { "chat", "completions", "embeddings" };

    public TestLanguageModelConnectionValidator()
    {
        RuleFor(x => x.ServiceType)
            .NotEmpty()
            .Must(BeAValidServiceType)
            .WithMessage($"ServiceType must be one of: {string.Join(", ", ValidServiceTypes)}");

        RuleFor(x => x.Endpoint)
            .NotEmpty()
            .MaximumLength(500)
            .Must(BeAValidUrl)
            .WithMessage("Endpoint must be a valid URL.");

        RuleFor(x => x.AuthMethod)
            .NotEmpty()
            .Must(BeAValidAuthMethod)
            .WithMessage($"AuthMethod must be one of: {string.Join(", ", ValidAuthMethods)}");

        RuleFor(x => x.ApiKey)
            .NotEmpty()
            .WithMessage("ApiKey is required when using ApiKey authentication.")
            .When(x => x.AuthMethod == "ApiKey");

        RuleFor(x => x.DeploymentName)
            .NotEmpty()
            .MaximumLength(200)
            .WithMessage("DeploymentName is required for Azure OpenAI.")
            .When(x => x.ServiceType == "AzureOpenAI");

        RuleFor(x => x.ApiVersion)
            .MaximumLength(50);

        RuleFor(x => x.ApiSurface)
            .Must(BeAValidApiSurface)
            .WithMessage($"ApiSurface must be one of: {string.Join(", ", ValidApiSurfaces)}")
            .When(x => !string.IsNullOrEmpty(x.ApiSurface));
    }

    private static bool BeAValidUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && 
        (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    private static bool BeAValidServiceType(string? serviceType) =>
        !string.IsNullOrEmpty(serviceType) && ValidServiceTypes.Contains(serviceType);

    private static bool BeAValidAuthMethod(string? authMethod) =>
        !string.IsNullOrEmpty(authMethod) && ValidAuthMethods.Contains(authMethod);

    private static bool BeAValidApiSurface(string? apiSurface) =>
        string.IsNullOrEmpty(apiSurface) || ValidApiSurfaces.Contains(apiSurface);
}
