using Ecommerce.Common.V1;
using Ecommerce.Rpc.Order.V1;

namespace Ecommerce.ApiGateway.Api.Contracts;

internal static class OrderMapping
{
    private const decimal NanosPerUnit = 1_000_000_000m;
    private const decimal BasisPointsPerPercent = 100m;

    public static OrderDto ToDto(this Order order) => new(
        OrderId: order.OrderId,
        CustomerId: order.CustomerId,
        Status: ToDto(order.Status),
        Items: order.Items.Select(ToDto).ToList(),
        Total: ToDto(order.Total),
        PlacedAt: order.PlacedAt.ToDateTimeOffset());

    private static OrderItemDto ToDto(OrderItem item) => new(
        ProductId: item.ProductId,
        ProductName: item.ProductName,
        Sku: item.Sku,
        UnitPrice: ToDto(item.UnitPrice),
        // 1000 basis points reaches the client as 10.00, not as 1000 or 0.1.
        TaxRatePercent: item.TaxRateBasisPoints / BasisPointsPerPercent,
        Quantity: item.Quantity);

    /// decimal, never double: this value is a price a customer is shown.
    private static MoneyDto ToDto(Money money) => new(
        Amount: money.Units + (money.Nanos / NanosPerUnit),
        Currency: money.CurrencyCode);

    /// Mapped explicitly rather than by ToString(). An enum value added to the contract
    /// and not handled here throws at the gateway, which is where the omission is cheap
    /// to notice — rather than reaching a client as the raw protobuf name.
    private static string ToDto(OrderStatus status) => status switch
    {
        OrderStatus.Pending => "pending",
        OrderStatus.Confirmed => "confirmed",
        OrderStatus.Cancelled => "cancelled",
        _ => throw new InvalidOperationException(
            $"Order status '{status}' has no public representation. Add one when the contract adds the value."),
    };
}
