using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tyto.Api.Domain.Configs;

/// <summary>
/// Centralizes (de)serialization of connection <c>Config</c> JSON payloads. Uses camelCase property
/// names and string enums so the stored JSON is stable and human-readable, and ignores nulls to keep
/// payloads compact.
/// </summary>
public static class ConnectionConfigSerializer
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>Deserializes a stored Config string into a typed config, or <c>null</c> when empty.</summary>
    public static T? Deserialize<T>(string? config) where T : class
        => string.IsNullOrWhiteSpace(config) ? null : JsonSerializer.Deserialize<T>(config, Options);

    /// <summary>Serializes a typed config into its canonical JSON string form.</summary>
    public static string Serialize<T>(T config) where T : class
        => JsonSerializer.Serialize(config, Options);
}
