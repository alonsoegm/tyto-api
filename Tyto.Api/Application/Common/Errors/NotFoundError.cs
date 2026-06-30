using Tyto.Api.Application.Common.Constants;
using FluentResults;

namespace Tyto.Api.Application.Common.Errors;

/// <summary>
/// Error type for entity not found scenarios.
/// </summary>
public class NotFoundError : Error
{
    public string Code { get; }

    public NotFoundError(string entityName, Guid id)
        : base($"{entityName} with ID '{id}' was not found.")
    {
        Code = ErrorCodes.NotFound;
    }

    public NotFoundError(string message)
        : base(message)
    {
        Code = ErrorCodes.NotFound;
    }
}
