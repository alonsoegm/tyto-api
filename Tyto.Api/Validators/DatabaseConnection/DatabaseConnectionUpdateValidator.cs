using FluentValidation;
using Tyto.Api.Application.DTOs.DatabaseConnection;

namespace Tyto.Api.Validators.DatabaseConnection;

/// <summary>
/// Validates <see cref="DatabaseConnectionUpdateDto"/> base fields. ConnectionType cannot be changed;
/// Config payload shape is validated per type by the service. Updates against internal connections are
/// rejected by the service before validation.
/// </summary>
public class DatabaseConnectionUpdateValidator : AbstractValidator<DatabaseConnectionUpdateDto>
{
    public DatabaseConnectionUpdateValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.Config).NotNull().WithMessage("Configuration is required.");
    }
}
