using Ecommerce.OrderService.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.OrderService.Infrastructure.Persistence;

public sealed class OrderRepository(OrderDbContext context) : IOrderRepository
{
    public async Task<Order?> FindAsync(OrderId id, CancellationToken cancellationToken = default) =>
        await context.Orders.FirstOrDefaultAsync(order => order.Id == id, cancellationToken);

    public void Add(Order order) => context.Orders.Add(order);
}
