using Ecommerce.OrderService.Application.Abstractions;
using Ecommerce.OrderService.Domain.Orders;

namespace Ecommerce.OrderService.Application.Orders.PlaceOrder;

/// Orchestrates; it does not decide. Every rule about what makes an order valid lives in
/// Order.Place, so the same rules apply however an order arrives — this handler, a saga
/// step in month 3, or a data fix.
public sealed class PlaceOrderCommandHandler(IOrderRepository orders, TimeProvider clock)
    : ICommandHandler<PlaceOrderCommand, Guid>
{
    public Task<Result<Guid>> Handle(PlaceOrderCommand command, CancellationToken cancellationToken)
    {
        var address = new Address(
            command.ShippingAddress.Line1,
            command.ShippingAddress.Line2,
            command.ShippingAddress.City,
            command.ShippingAddress.PostalCode,
            command.ShippingAddress.Country);

        var items = command.Lines
            .Select(line => OrderItem.Snapshot(
                new ProductId(line.ProductId),
                line.ProductName,
                new Sku(line.Sku),
                Money.Of(line.UnitPriceAmount, line.Currency),
                TaxRate.FromBasisPoints(line.TaxRateBasisPoints),
                line.Quantity))
            .ToList();

        var order = Order.Place(
            OrderId.New(),
            new CustomerId(command.CustomerId),
            address,
            items,
            clock.GetUtcNow());

        orders.Add(order);

        // No SaveChanges here — the transaction behaviour commits once, after this returns.
        return Task.FromResult(Result<Guid>.Success(order.Id.Value));
    }
}
