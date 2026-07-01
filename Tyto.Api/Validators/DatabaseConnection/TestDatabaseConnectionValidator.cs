using FluentValidation;
using Tyto.Api.Application.DTOs.DatabaseConnection;
using Tyto.Api.Domain.Enums;

namespace Tyto.Api.Validators.DatabaseConnection;

/// <summary>
/// Validates <see cref="TestDatabaseConnectionDto"/> for ad-hoc connection tests. The Config payload
/// shape is validated per type by the service; InternalSql and CosmosDb are handled there with a
/// controlled result.
/// </summary>
public class TestDatabaseConnectionValidator : AbstractValidator<TestDatabaseConnectionDto>
{
    private static readonly ConnectionType[] Testable = [ConnectionType.Salesforce, ConnectionType.Dataverse];

    public TestDatabaseConnectionValidator()
    {
        RuleFor(x => x.Config)
            .NotNull().WithMessage("Configuration is required to test an external connection.")
            .When(x => Testable.Contains(x.ConnectionType));
    }
}
