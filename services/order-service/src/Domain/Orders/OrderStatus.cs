namespace Ecommerce.OrderService.Domain.Orders;

/// Mirrors rpc.order.v1.OrderStatus deliberately. The domain owns the meaning; the
/// contract exposes it. If these two ever disagree, the contract is the one that has to
/// change, through a new proto version.
public enum OrderStatus
{
    Pending = 1,
    Confirmed = 2,
    Cancelled = 3,
}
