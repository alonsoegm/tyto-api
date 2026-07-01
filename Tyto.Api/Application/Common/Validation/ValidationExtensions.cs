using FluentResults;
using FluentValidation;
using Tyto.Api.Application.Common.Errors;

namespace Tyto.Api.Application.Common.Validation;

/// <summary>
/// Extensions for integrating FluentValidation with FluentResults.
/// </summary>
public static class ValidationExtensions
{
    /// <summary>
    /// Validates an instance and returns a Result instead of throwing ValidationException.
    /// </summary>
    /// <typeparam name="T">The type being validated</typeparam>
    /// <param name="validator">The validator instance</param>
    /// <param name="instance">The instance to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result with validated instance on success, or ValidationError on failure</returns>
    public static async Task<Result<T>> ValidateToResultAsync<T>(
        this IValidator<T> validator,
        T instance,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await validator.ValidateAsync(instance, cancellationToken);

        if (validationResult.IsValid)
        {
            return Result.Ok(instance);
        }

        // Group errors by property name
        var fieldErrors = validationResult.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray()
            );

        var message = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));

        return Result.Fail<T>(new ValidationError(message, fieldErrors));
    }
}
