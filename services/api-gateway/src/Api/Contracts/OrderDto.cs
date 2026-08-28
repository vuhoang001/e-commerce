namespace Ecommerce.ApiGateway.Api.Contracts;

/// The public JSON shape of an order.
///
/// Deliberately not the protobuf type. The internal contract and the public one change
/// for different reasons and at different speeds, and protobuf's own JSON mapping leaks
/// details a browser has no use for — money as units-plus-nanos, a tax rate in basis
/// points, enum names like ORDER_STATUS_CONFIRMED. Translating here is the gateway
/// earning its place; PLAN.md section 3b calls this option B.
public sealed record OrderDto(
    string OrderId,
    string CustomerId,
    string Status,
    IReadOnlyList<OrderItemDto> Items,
    MoneyDto Total,
    DateTimeOffset PlacedAt);

public sealed record OrderItemDto(
    string ProductId,
    string ProductName,
    string Sku,
    MoneyDto UnitPrice,
    decimal TaxRatePercent,
    int Quantity);

/// Money as a client expects to render it: 18.99 USD, not 18 units and 990000000 nanos.
public sealed record MoneyDto(decimal Amount, string Currency);
