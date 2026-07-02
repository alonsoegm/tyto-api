using FluentValidation;
using Tyto.Api.Application.DTOs.Configuration;

namespace Tyto.Api.Validators.Configuration;

/// <summary>Validates <see cref="ConfigurationCreateDto"/>. Ensures referenced entity IDs and prompt settings are valid.</summary>
public class ConfigurationCreateValidator : AbstractValidator<ConfigurationCreateDto>
{
    public ConfigurationCreateValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
        // TargetObject is required only for external connections; for internal (Internal SQL) it is
        // resolved server-side. That connection-type-dependent rule lives in ConfigurationService,
        // which has the DatabaseConnection loaded. Here we only bound the length.
        RuleFor(x => x.TargetObject).MaximumLength(200);
        RuleFor(x => x.LanguageModelId).NotEmpty();
        RuleFor(x => x.DatabaseConnectionId).NotEmpty();
        RuleFor(x => x.SystemPrompt).MaximumLength(5000);
        RuleFor(x => x.UserPromptTemplate).MaximumLength(5000);
        RuleFor(x => x.MaxTokens).InclusiveBetween(1, 128000);
        RuleFor(x => x.Temperature).InclusiveBetween(0.0, 2.0);
        RuleFor(x => x.MaxUploadSizeMB).InclusiveBetween(1, 100);
        RuleFor(x => x.AcceptedFileTypes).NotNull();
        RuleFor(x => x.MappedFields).NotEmpty().WithMessage("At least one mapped field is required.");
        RuleForEach(x => x.MappedFields).ChildRules(f =>
        {
            f.RuleFor(x => x.FieldName).NotEmpty().MaximumLength(200);
            f.RuleFor(x => x.DisplayLabel).NotEmpty().MaximumLength(200);
        });
    }
}
