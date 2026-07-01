using FluentResults;
using Tyto.Api.Application.Common.Constants;

namespace Tyto.Api.Application.Common.Errors;

/// <summary>
/// Error type for actions that are not permitted on a resource (e.g., modifying or testing a
/// system-managed connection). Maps to HTTP 403.
/// </summary>
public class ForbiddenError : Error
{
    public string Code { get; }

    public ForbiddenError(string message)
        : base(message)
    {
        Code = ErrorCodes.Forbidden;
    }
}
