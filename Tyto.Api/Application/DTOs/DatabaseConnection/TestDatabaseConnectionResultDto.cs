namespace Tyto.Api.Application.DTOs.DatabaseConnection;

public record TestDatabaseConnectionResultDto(
    bool IsSuccess,
    string Message,
    int? StatusCode
);
