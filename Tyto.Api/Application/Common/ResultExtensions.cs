using FluentResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Tyto.Api.Application.Common.Constants;
using Tyto.Api.Application.Common.Errors;

namespace Tyto.Api.Application.Common;

/// <summary>
/// Extensions for converting FluentResults to ApiResponse.
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Converts a Result{T} to ApiResponse{T}.
    /// </summary>
    public static ApiResponse<T> ToApiResponse<T>(this Result<T> result)
    {
        if (result.IsSuccess)
        {
            return ApiResponse<T>.Ok(result.Value);
        }

        var error = result.Errors.FirstOrDefault();
        if (error == null)
        {
            return ApiResponse<T>.Fail(ErrorCodes.InternalError, "An unknown error occurred.");
        }

        // Extract error code and message from custom error types
        var (code, message) = error switch
        {
            NotFoundError notFoundError => (notFoundError.Code, notFoundError.Message),
            ConflictError conflictError => (conflictError.Code, conflictError.Message),
            ForbiddenError forbiddenError => (forbiddenError.Code, forbiddenError.Message),
            ValidationError validationError => (validationError.Code, validationError.Message),
            InternalError internalError => (internalError.Code, internalError.Message),
            _ => (ErrorCodes.InternalError, error.Message)
        };

        return ApiResponse<T>.Fail(code, message);
    }

    /// <summary>
    /// Converts a non-generic Result to ApiResponse{object}.
    /// </summary>
    public static ApiResponse<object> ToApiResponse(this Result result)
    {
        if (result.IsSuccess)
        {
            return ApiResponse<object>.Ok(new { });
        }

        var error = result.Errors.FirstOrDefault();
        if (error == null)
        {
            return ApiResponse<object>.Fail(ErrorCodes.InternalError, "An unknown error occurred.");
        }

        // Extract error code and message from custom error types
        var (code, message) = error switch
        {
            NotFoundError notFoundError => (notFoundError.Code, notFoundError.Message),
            ConflictError conflictError => (conflictError.Code, conflictError.Message),
            ForbiddenError forbiddenError => (forbiddenError.Code, forbiddenError.Message),
            ValidationError validationError => (validationError.Code, validationError.Message),
            InternalError internalError => (internalError.Code, internalError.Message),
            _ => (ErrorCodes.InternalError, error.Message)
        };

        return ApiResponse<object>.Fail(code, message);
    }

    /// <summary>
    /// Gets the error code from a Result's first error.
    /// </summary>
    public static string GetErrorCode(this Result result)
    {
        var error = result.Errors.FirstOrDefault();
        return error switch
        {
            NotFoundError notFoundError => notFoundError.Code,
            ConflictError conflictError => conflictError.Code,
            ForbiddenError forbiddenError => forbiddenError.Code,
            ValidationError validationError => validationError.Code,
            InternalError internalError => internalError.Code,
            _ => ErrorCodes.InternalError
        };
    }

    /// <summary>
    /// Gets the error code from a Result{T}'s first error.
    /// </summary>
    public static string GetErrorCode<T>(this Result<T> result)
    {
        var error = result.Errors.FirstOrDefault();
        return error switch
        {
            NotFoundError notFoundError => notFoundError.Code,
            ConflictError conflictError => conflictError.Code,
            ForbiddenError forbiddenError => forbiddenError.Code,
            ValidationError validationError => validationError.Code,
            InternalError internalError => internalError.Code,
            _ => ErrorCodes.InternalError
        };
    }

    /// <summary>
    /// Converts a failed Result{T} to an IActionResult with a ProblemDetails body.
    /// Maps error types to the appropriate HTTP status code automatically.
    /// </summary>
    public static ActionResult ToErrorResult<T>(this Result<T> result, ControllerBase controller)
        => BuildErrorResult(result.Errors.FirstOrDefault(), controller);

    /// <summary>
    /// Converts a failed Result to an ActionResult with a ProblemDetails body.
    /// Maps error types to the appropriate HTTP status code automatically.
    /// </summary>
    public static ActionResult ToErrorResult(this Result result, ControllerBase controller)
        => BuildErrorResult(result.Errors.FirstOrDefault(), controller);

    private static ActionResult BuildErrorResult(IError? error, ControllerBase controller)
    {
        if (error is ValidationError { FieldErrors: { Count: > 0 } fieldErrors } ve)
        {
            var modelState = new ModelStateDictionary();
            foreach (var (field, errors) in fieldErrors)
                foreach (var err in errors)
                    modelState.AddModelError(field, err);

            var validationDetails = controller.ProblemDetailsFactory.CreateValidationProblemDetails(
                controller.HttpContext, modelState, detail: ve.Message);
            validationDetails.Extensions["code"] = ErrorCodes.ValidationError;
            return new BadRequestObjectResult(validationDetails);
        }

        var (statusCode, code, message) = error switch
        {
            NotFoundError e => (StatusCodes.Status404NotFound, e.Code, e.Message),
            ConflictError e => (StatusCodes.Status409Conflict, e.Code, e.Message),
            ForbiddenError e => (StatusCodes.Status403Forbidden, e.Code, e.Message),
            ValidationError e => (StatusCodes.Status400BadRequest, e.Code, e.Message),
            InternalError e => (StatusCodes.Status500InternalServerError, e.Code, e.Message),
            _ => (StatusCodes.Status500InternalServerError, ErrorCodes.InternalError, "An unexpected error occurred.")
        };

        var details = controller.ProblemDetailsFactory.CreateProblemDetails(
            controller.HttpContext, statusCode: statusCode, detail: message);
        details.Extensions["code"] = code;
        return new ObjectResult(details) { StatusCode = statusCode };
    }
}
