using System.Text.Json.Nodes;
using Tyto.Api.Domain.Enums;

namespace Tyto.Api.Application.DTOs.DatabaseConnection;

/// <summary>
/// Ad-hoc connection test request from form values (without saving). <see cref="Config"/> carries the
/// connection-type-specific payload with plaintext secrets.
/// </summary>
public record TestDatabaseConnectionDto(
    ConnectionType ConnectionType,
    JsonObject? Config
);
