using Tyto.Api.Application.DTOs.MappedField;
using FluentValidation;

namespace Tyto.Api.Validators.MappedField;

/// <summary>Validates <see cref="MappedFieldUpdateDto"/>. Ensures FieldName and sort order are valid.</summary>
public class MappedFieldUpdateValidator : AbstractValidator<MappedFieldUpdateDto>
{
    public MappedFieldUpdateValidator()
    {
        RuleFor(x => x.FieldName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DisplayLabel).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ExtractionHint).MaximumLength(1000);
        RuleFor(x => x.DefaultValue).MaximumLength(500);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}
