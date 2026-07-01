using FluentValidation;
using Tyto.Api.Application.DTOs.Configuration;

namespace Tyto.Api.Validators.Configuration;

/// <summary>Validates <see cref="ConfigurationUpdateDto"/>. Same rules as create plus status field.</summary>
public class ConfigurationUpdateValidator : AbstractValidator<ConfigurationUpdateDto>
{
    public ConfigurationUpdateValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.TargetObject).NotEmpty().MaximumLength(200);
        RuleFor(x => x.LanguageModelId).NotEmpty();
        RuleFor(x => x.DatabaseConnectionId).NotEmpty();
        RuleFor(x => x.SystemPrompt).MaximumLength(5000);
        RuleFor(x => x.UserPromptTemplate).MaximumLength(5000);
        RuleFor(x => x.MaxTokens).InclusiveBetween(1, 128000);
        RuleFor(x => x.Temperature).InclusiveBetween(0.0, 2.0);
    }
}
