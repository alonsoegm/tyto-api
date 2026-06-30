namespace Tyto.Api.Application.DTOs.RunHistory;

public record RunHistoryResponseDto(
    Guid Id,
    Guid ConfigurationId,
    string ConfigurationName,
    DateTime StartedAt,
    DateTime? CompletedAt,
    bool Success,
    string? ErrorMessage,
    int DocumentsProcessed,
    int RecordsCreated,
    int RecordsUpdated,
    int RecordsFailed,
    string TriggeredBy,
    DateTime CreatedAt
);
