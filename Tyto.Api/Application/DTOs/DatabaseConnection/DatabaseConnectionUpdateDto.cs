using System.Text.Json.Nodes;

namespace Tyto.Api.Application.DTOs.DatabaseConnection;

/// <summary>
/// Request to update an external database connection. ConnectionType cannot be changed.
/// Secret fields inside <see cref="Config"/> may be sent as the masked placeholder to preserve the
/// stored value.
/// </summary>
public record DatabaseConnectionUpdateDto(
    string Name,
    string Description,
    JsonObject? Config
);
