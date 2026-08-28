using Ecommerce.OrderService.Domain.Orders;

namespace Ecommerce.OrderService.Domain.UnitTests;

/// Plain builders, no framework. A domain test that needs setup machinery is usually
/// telling you the aggregate is hard to construct correctly.
internal static class OrderFixtures
{
    public static readonly DateTimeOffset Now = new(2026, 8, 28, 9, 30, 0, TimeSpan.Zero);

    public static Address AnAddress() =>
        new("221B Baker Street", null, "London", "NW1 6XE", "GB");

    public static OrderItem AnItem(
        string productId = "product-7",
        decimal unitPrice = 18.99m,
        string currency = "USD",
        int taxBasisPoints = 1000,
        int quantity = 2) =>
        OrderItem.Snapshot(
            new ProductId(productId),
            "Cafetiere, 1 litre",
            new Sku("KIT-CAF-1L"),
            Money.Of(unitPrice, currency),
            TaxRate.FromBasisPoints(taxBasisPoints),
            quantity);

    public static Order AnOrder(params OrderItem[] items) =>
        Order.Place(
            OrderId.New(),
            new CustomerId(Guid.NewGuid()),
            AnAddress(),
            items.Length == 0 ? [AnItem()] : items,
            Now);
}
