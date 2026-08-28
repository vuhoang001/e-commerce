using Ecommerce.Common.V1;
using Ecommerce.Rpc.Order.V1;

namespace Ecommerce.ApiGateway.Orders;

internal static class OrderMapping
{
    private const decimal NanosPerUnit = 1_000_000_000m;
    private const decimal BasisPointsPerPercent = 100m;

    public static OrderResponse ToResponse(this Order order) => new(
        OrderId: order.OrderId,
        CustomerId: order.CustomerId,
        Status: ToResponse(order.Status),
        Items: order.Items.Select(ToResponse).ToList(),
        Total: ToResponse(order.Total),
        PlacedAt: order.PlacedAt.ToDateTimeOffset());

    private static OrderItemResponse ToResponse(OrderItem item) => new(
        ProductId: item.ProductId,
        ProductName: item.ProductName,
        Sku: item.Sku,
        UnitPrice: ToResponse(item.UnitPrice),
        // 1000 basis points reaches the client as 10.00, not as 1000 or 0.1.
        TaxRatePercent: item.TaxRateBasisPoints / BasisPointsPerPercent,
        Quantity: item.Quantity);

    /// decimal, never double: this value is a price a customer is shown.
    private static MoneyResponse ToResponse(Money money) => new(
        Amount: money.Units + (money.Nanos / NanosPerUnit),
        Currency: money.CurrencyCode);

    /// Mapped explicitly rather than by ToString(). An enum value added to the contract
    /// and not handled here throws at the gateway, which is where the omission is cheap
    /// to notice — rather than reaching a client as the raw protobuf name.
    private static string ToResponse(OrderStatus status) => status switch
    {
        OrderStatus.Pending => "pending",
        OrderStatus.Confirmed => "confirmed",
        OrderStatus.Cancelled => "cancelled",
        _ => throw new InvalidOperationException(
            $"Order status '{status}' has no public representation. Add one when the contract adds the value."),
    };
}
