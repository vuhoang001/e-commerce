using Ecommerce.Common.V1;
using Ecommerce.Rpc.Order.V1;

namespace Ecommerce.OrderService.Api.IntegrationTests;

internal static class Requests
{
    public static PlaceOrderRequest AnOrder(
        string currency = "USD",
        long units = 18,
        int nanos = 990_000_000,
        int taxBasisPoints = 1000,
        int quantity = 2) =>
        new()
        {
            CustomerId = Guid.NewGuid().ToString(),
            ShippingAddress = new Address
            {
                Line1 = "221B Baker Street",
                City = "London",
                PostalCode = "NW1 6XE",
                Country = "GB",
            },
            Lines =
            {
                new PlaceOrderLine
                {
                    ProductId = "product-7",
                    ProductName = "Cafetiere, 1 litre",
                    Sku = "KIT-CAF-1L",
                    UnitPrice = new Money { CurrencyCode = currency, Units = units, Nanos = nanos },
                    TaxRateBasisPoints = taxBasisPoints,
                    Quantity = quantity,
                },
            },
        };
}
