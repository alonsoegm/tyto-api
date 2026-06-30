namespace Tyto.Api.Domain.Entities;

public class RunHistory : BaseEntity
{
    public Guid ConfigurationId { get; set; }
    public Configuration Configuration { get; set; } = null!;

    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int DocumentsProcessed { get; set; }
    public int RecordsCreated { get; set; }
    public int RecordsUpdated { get; set; }
    public int RecordsFailed { get; set; }
    public string? RawInput { get; set; }
    public string? RawOutput { get; set; }
    public string TriggeredBy { get; set; } = string.Empty;
}
