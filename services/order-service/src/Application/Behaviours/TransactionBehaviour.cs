using Ecommerce.OrderService.Domain.Abstractions;
using Ecommerce.OrderService.Application.Abstractions;
using MediatR;

namespace Ecommerce.OrderService.Application.Behaviours;

/// Commits once, after the handler returns, and only for commands.
///
/// Handlers never call SaveChanges themselves. That is what keeps "one aggregate per
/// transaction" true: a handler that saves twice has quietly made two transactions, and
/// nothing in a code review would show it.
public sealed class TransactionBehaviour<TRequest, TResponse>(IUnitOfWork unitOfWork)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICommandMarker
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var response = await next();

        // Domain events raised during the handler are still sitting on the aggregate. The
        // outbox in month 2 reads them here, inside the same transaction, which is what
        // makes "saved but never published" impossible. Until then they are simply saved.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return response;
    }
}
