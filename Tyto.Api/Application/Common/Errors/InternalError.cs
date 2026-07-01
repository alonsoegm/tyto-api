using FluentResults;
using Tyto.Api.Application.Common.Constants;

namespace Tyto.Api.Application.Common.Errors;

/// <summary>
/// Error type for unexpected internal errors (e.g., database exceptions).
/// </summary>
public class InternalError : Error
{
    public string Code { get; }

    public InternalError(string message)
        : base(message)
    {
        Code = ErrorCodes.InternalError;
    }

    public InternalError(string message, Exception exception)
        : base(message)
    {
        Code = ErrorCodes.InternalError;
        CausedBy(exception);
    }
}
