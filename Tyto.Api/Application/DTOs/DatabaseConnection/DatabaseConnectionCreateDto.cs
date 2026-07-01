using System.Text.Json.Nodes;
using Tyto.Api.Domain.Enums;

namespace Tyto.Api.Application.DTOs.DatabaseConnection;

/// <summary>
/// Request to create an external database connection. <see cref="Config"/> carries the
/// connection-type-specific payload (Salesforce/Dataverse). Internal connections cannot be created
/// through this endpoint.
/// </summary>
public record DatabaseConnectionCreateDto(
    string Name,
    string Description,
    ConnectionType ConnectionType,
    JsonObject? Config
);
