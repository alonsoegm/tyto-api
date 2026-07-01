using FluentResults;
using Tyto.Api.Application.Common.Constants;

namespace Tyto.Api.Application.Common.Errors;

/// <summary>
/// Error type for business rule conflicts (e.g., duplicate names, invalid state).
/// </summary>
public class ConflictError : Error
{
    public string Code { get; }

    public ConflictError(string message)
        : base(message)
    {
        Code = ErrorCodes.Conflict;
    }
}
