using Ecommerce.OrderService.Application.Abstractions;
using Ecommerce.OrderService.Domain.Orders;

namespace Ecommerce.OrderService.Application.Orders.GetOrder;

public sealed record GetOrderQuery(Guid OrderId) : IQuery<Order>;
