using Tyto.Api.Application.Common.Constants;
using FluentResults;

namespace Tyto.Api.Application.Common.Errors;

/// <summary>
/// Error type for validation failures.
/// </summary>
public class ValidationError : Error
{
    public string Code { get; }
    public Dictionary<string, string[]>? FieldErrors { get; }

    public ValidationError(string message)
        : base(message)
    {
        Code = ErrorCodes.ValidationError;
    }

    public ValidationError(string message, Dictionary<string, string[]> fieldErrors)
        : base(message)
    {
        Code = ErrorCodes.ValidationError;
        FieldErrors = fieldErrors;
    }
}
