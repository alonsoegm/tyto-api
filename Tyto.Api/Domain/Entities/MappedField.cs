using Tyto.Api.Domain.Enums;

namespace Tyto.Api.Domain.Entities;

public class MappedField : BaseEntity
{
    public Guid ConfigurationId { get; set; }
    public Configuration Configuration { get; set; } = null!;

    public string FieldName { get; set; } = string.Empty;
    public string DisplayLabel { get; set; } = string.Empty;
    public FieldType FieldType { get; set; }
    public RequirementLevel RequirementLevel { get; set; }
    public string? ExtractionHint { get; set; }
    public string? DefaultValue { get; set; }
    public int SortOrder { get; set; }

    // Self-referencing parent/child
    public Guid? ParentFieldId { get; set; }
    public MappedField? ParentField { get; set; }
    public ICollection<MappedField> ChildFields { get; set; } = new List<MappedField>();
}
