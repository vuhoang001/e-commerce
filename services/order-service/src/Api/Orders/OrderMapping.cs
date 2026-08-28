using Google.Protobuf.WellKnownTypes;
using DomainOrder = Ecommerce.OrderService.Domain.Orders.Order;
using DomainOrderItem = Ecommerce.OrderService.Domain.Orders.OrderItem;
using DomainMoney = Ecommerce.OrderService.Domain.Orders.Money;
using DomainStatus = Ecommerce.OrderService.Domain.Orders.OrderStatus;
using ProtoOrder = Ecommerce.Rpc.Order.V1.Order;
using ProtoOrderItem = Ecommerce.Rpc.Order.V1.OrderItem;
using ProtoMoney = Ecommerce.Common.V1.Money;
using ProtoStatus = Ecommerce.Rpc.Order.V1.OrderStatus;

namespace Ecommerce.OrderService.Api.Orders;

/// Translates the aggregate into the wire contract.
///
/// This mapping is the reason order-service now has what the gateway has had all along:
/// two models that change for different reasons. The aggregate answers to the business,
/// the proto message answers to every other service, and neither should drag the other
/// along when it changes.
internal static class OrderMapping
{
    private const decimal NanosPerUnit = 1_000_000_000m;

    public static ProtoOrder ToProto(this DomainOrder order)
    {
        var message = new ProtoOrder
        {
            OrderId = order.Id.Value.ToString(),
            CustomerId = order.CustomerId.Value.ToString(),
            Status = ToProto(order.Status),
            Total = ToProto(order.Total),
            PlacedAt = Timestamp.FromDateTimeOffset(order.PlacedAt),
        };

        message.Items.AddRange(order.Items.Select(ToProto));

        return message;
    }

    private static ProtoOrderItem ToProto(DomainOrderItem item) => new()
    {
        ProductId = item.ProductId.Value,
        ProductName = item.ProductName,
        Sku = item.Sku.Value,
        UnitPrice = ToProto(item.UnitPrice),
        TaxRateBasisPoints = item.TaxRate.BasisPoints,
        Quantity = item.Quantity,
    };

    /// The domain holds an exact decimal; the wire holds whole units plus billionths.
    /// Neither is floating point, so the conversion is lossless in both directions for any
    /// amount this service can store.
    private static ProtoMoney ToProto(DomainMoney money)
    {
        var units = decimal.Truncate(money.Amount);

        return new ProtoMoney
        {
            CurrencyCode = money.Currency,
            Units = (long)units,
            Nanos = (int)decimal.Round((money.Amount - units) * NanosPerUnit),
        };
    }

    public static DomainMoney ToDomain(this ProtoMoney money) =>
        DomainMoney.Of(money.Units + (money.Nanos / NanosPerUnit), money.CurrencyCode);

    /// Mapped explicitly. A status added to the domain and forgotten here fails loudly at
    /// the boundary rather than reaching a caller as a default enum value.
    private static ProtoStatus ToProto(DomainStatus status) => status switch
    {
        DomainStatus.Pending => ProtoStatus.Pending,
        DomainStatus.Confirmed => ProtoStatus.Confirmed,
        DomainStatus.Cancelled => ProtoStatus.Cancelled,
        _ => throw new InvalidOperationException($"Order status '{status}' has no contract representation."),
    };
}
