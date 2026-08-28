namespace Ecommerce.OrderService.Domain.Orders;

/// One repository per aggregate root, never per table. There is no OrderItemRepository:
/// a line is reached through its order or not at all.
public interface IOrderRepository
{
    Task<Order?> FindAsync(OrderId id, CancellationToken cancellationToken = default);

    void Add(Order order);
}
