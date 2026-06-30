using Tyto.Api.Domain.Enums;

namespace Tyto.Api.Application.DTOs.MappedField;

public record MappedFieldUpdateDto(
    string FieldName,
    string DisplayLabel,
    FieldType FieldType,
    RequirementLevel RequirementLevel,
    string? ExtractionHint,
    string? DefaultValue,
    int SortOrder,
    Guid? ParentFieldId
);
