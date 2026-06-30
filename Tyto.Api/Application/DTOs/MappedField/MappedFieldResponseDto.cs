using Tyto.Api.Domain.Enums;

namespace Tyto.Api.Application.DTOs.MappedField;

public record MappedFieldResponseDto(
    Guid Id,
    Guid ConfigurationId,
    string FieldName,
    string DisplayLabel,
    FieldType FieldType,
    RequirementLevel RequirementLevel,
    string? ExtractionHint,
    string? DefaultValue,
    int SortOrder,
    Guid? ParentFieldId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string CreatedBy,
    string UpdatedBy
);
