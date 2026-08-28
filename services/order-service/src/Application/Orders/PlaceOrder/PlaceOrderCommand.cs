using Ecommerce.OrderService.Application.Abstractions;

namespace Ecommerce.OrderService.Application.Orders.PlaceOrder;

/// Places an order and returns its id.
///
/// KNOWN GAP, closed in month 2: the caller supplies the product name, price and tax rate.
/// That is fine for a walking skeleton and unacceptable in production — a client could name
/// its own price. PLAN.md month 2 adds order-service's local product read model and price
/// validation at checkout, after which this command carries only product ids and
/// quantities and the server reads the rest.
public sealed record PlaceOrderCommand(
    Guid CustomerId,
    PlaceOrderAddress ShippingAddress,
    IReadOnlyList<PlaceOrderLine> Lines) : ICommand<Guid>;

public sealed record PlaceOrderAddress(
    string Line1,
    string? Line2,
    string City,
    string PostalCode,
    string Country);

public sealed record PlaceOrderLine(
    string ProductId,
    string ProductName,
    string Sku,
    decimal UnitPriceAmount,
    string Currency,
    int TaxRateBasisPoints,
    int Quantity);
