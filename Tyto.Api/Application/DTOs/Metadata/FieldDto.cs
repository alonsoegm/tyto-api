namespace Tyto.Api.Application.DTOs.Metadata;

/// <summary>
/// Provider-agnostic representation of a field (column) belonging to an entity.
/// <see cref="Type"/> is the provider's native type name (not normalized in the MVP).
/// </summary>
public record FieldDto(string Id, string Name, string Type);
