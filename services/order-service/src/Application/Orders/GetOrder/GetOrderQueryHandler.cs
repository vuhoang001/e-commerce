using Ecommerce.OrderService.Application.Abstractions;
using Ecommerce.OrderService.Domain.Orders;

namespace Ecommerce.OrderService.Application.Orders.GetOrder;

public sealed class GetOrderQueryHandler(IOrderRepository orders)
    : IQueryHandler<GetOrderQuery, Order>
{
    public static readonly Error NotFound = new("order.not_found", "No order with that id.");

    public async Task<Result<Order>> Handle(GetOrderQuery query, CancellationToken cancellationToken)
    {
        var order = await orders.FindAsync(new OrderId(query.OrderId), cancellationToken);

        // Absence is an ordinary answer, not an exception: the caller asked a reasonable
        // question and this is the reasonable reply.
        return order is null
            ? Result<Order>.Failure(NotFound)
            : Result<Order>.Success(order);
    }
}
