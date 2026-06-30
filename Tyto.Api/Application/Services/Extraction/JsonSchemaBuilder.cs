using System.Text.Json.Nodes;
using Tyto.Api.Domain.Entities;
using Tyto.Api.Domain.Enums;

namespace Tyto.Api.Application.Services.Extraction;

/// <summary>
/// Builds a strict JSON Schema from a configuration's mapped-field tree, for use with
/// OpenAI structured outputs. Ported from the legacy <c>DropSchema.ToJsonSchema()</c>.
///
/// Strict mode requires every property to be listed in <c>required</c> and
/// <c>additionalProperties: false</c>; optional fields are expressed as a nullable type
/// (e.g. <c>["string", "null"]</c>).
/// </summary>
public static class JsonSchemaBuilder
{
    /// <summary>Builds the root schema (a JSON string) from the top-level mapped fields.</summary>
    public static string Build(IReadOnlyList<MappedField> topLevelFields) =>
        BuildObject(topLevelFields).ToJsonString();

    private static JsonObject BuildObject(IEnumerable<MappedField> fields)
    {
        var properties = new JsonObject();
        var required = new JsonArray();
        // A JSON object cannot have duplicate keys; skip blanks and de-duplicate siblings
        // by name (defensive against configurations that contain duplicate mapped fields).
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var field in fields.OrderBy(f => f.SortOrder))
        {
            if (string.IsNullOrWhiteSpace(field.FieldName) || !seen.Add(field.FieldName))
                continue;

            properties[field.FieldName] = BuildField(field);
            required.Add(field.FieldName);
        }

        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required,
            ["additionalProperties"] = false,
        };
    }

    private static JsonObject BuildField(MappedField field)
    {
        // A field with children is modeled as a nested object.
        if (field.ChildFields is { Count: > 0 })
        {
            var nested = BuildObject(field.ChildFields);
            nested["description"] = BuildDescription(field, formatHint: null);
            return nested;
        }

        var (jsonType, formatHint) = MapType(field.FieldType);
        var nullable = field.RequirementLevel != RequirementLevel.Required;

        return new JsonObject
        {
            ["type"] = nullable
                ? (JsonNode)new JsonArray(JsonValue.Create(jsonType), JsonValue.Create("null"))
                : JsonValue.Create(jsonType),
            ["description"] = BuildDescription(field, formatHint),
        };
    }

    private static (string JsonType, string? FormatHint) MapType(FieldType fieldType) => fieldType switch
    {
        FieldType.Number => ("number", null),
        FieldType.Currency => ("number", null),
        FieldType.Boolean => ("boolean", null),
        FieldType.Date => ("string", "Format this field as YYYY-MM-DD. If the date is incomplete or missing, return null."),
        // Picklist and Lookup are treated as free strings in the MVP (no option/target metadata yet).
        _ => ("string", null),
    };

    private static string BuildDescription(MappedField field, string? formatHint)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(field.DisplayLabel))
            parts.Add(field.DisplayLabel);
        if (!string.IsNullOrWhiteSpace(field.ExtractionHint))
            parts.Add(field.ExtractionHint);
        if (!string.IsNullOrWhiteSpace(formatHint))
            parts.Add(formatHint);

        return string.Join(" ", parts);
    }
}
