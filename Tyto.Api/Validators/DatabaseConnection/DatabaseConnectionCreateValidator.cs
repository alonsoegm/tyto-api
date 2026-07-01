using FluentValidation;
using Tyto.Api.Application.DTOs.DatabaseConnection;
using Tyto.Api.Domain.Enums;

namespace Tyto.Api.Validators.DatabaseConnection;

/// <summary>
/// Validates <see cref="DatabaseConnectionCreateDto"/> base fields and enforces which connection
/// types can be created through the public API. Config payload shape is validated per type by the
/// service using <see cref="SalesforceConfigValidator"/> / <see cref="DataverseConfigValidator"/>.
/// </summary>
public class DatabaseConnectionCreateValidator : AbstractValidator<DatabaseConnectionCreateDto>
{
    private static readonly ConnectionType[] Creatable = [ConnectionType.Salesforce, ConnectionType.Dataverse];

    public DatabaseConnectionCreateValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);

        RuleFor(x => x.ConnectionType)
            .Must(type => Creatable.Contains(type))
            .WithMessage(dto => dto.ConnectionType switch
            {
                ConnectionType.InternalSql => "Internal SQL connections are system-managed and cannot be created.",
                ConnectionType.CosmosDb => "Azure Cosmos DB connections are coming soon and cannot be created yet.",
                _ => $"Unsupported connection type: {dto.ConnectionType}."
            });

        RuleFor(x => x.Config)
            .NotNull().WithMessage("Configuration is required for external connections.")
            .When(x => Creatable.Contains(x.ConnectionType));
    }
}
