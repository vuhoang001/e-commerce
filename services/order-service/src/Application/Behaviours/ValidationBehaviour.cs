using FluentValidation;
using MediatR;

namespace Ecommerce.OrderService.Application.Behaviours;

/// Runs before the handler, so a malformed request never reaches the domain.
///
/// This one throws rather than returning a failed Result, and that is deliberate. A Result
/// describes an outcome the domain considered and rejected — "no such order". A request
/// that does not even satisfy its own shape was never a valid question; it is translated to
/// INVALID_ARGUMENT at the API edge, where every other transport concern is handled.
public sealed class ValidationBehaviour<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);

        var failures = (await Task.WhenAll(
                validators.Select(validator => validator.ValidateAsync(context, cancellationToken))))
            .SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .ToList();

        if (failures.Count != 0)
        {
            throw new ValidationException(failures);
        }

        return await next();
    }
}
