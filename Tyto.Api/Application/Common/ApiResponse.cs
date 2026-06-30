namespace Tyto.Api.Application.Common;

public class ApiResponse<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public ApiError? Error { get; init; }

    private ApiResponse() { }

    public static ApiResponse<T> Ok(T data) =>
        new() { Success = true, Data = data, Error = null };

    public static ApiResponse<T> Fail(string code, string message) =>
        new() { Success = false, Data = default, Error = new ApiError(code, message) };
}
