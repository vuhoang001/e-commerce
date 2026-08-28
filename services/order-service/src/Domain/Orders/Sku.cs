using Ecommerce.OrderService.Domain.Abstractions;

namespace Ecommerce.OrderService.Domain.Orders;

/// A stock-keeping unit. A string with a format is a value object, not a primitive —
/// otherwise nothing stops a product name being passed where a SKU belongs.
///
/// A record class, not a struct: `default(Sku)` would hold a null string that no
/// constructor ever approved.
public sealed record Sku
{
    public Sku(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("A SKU cannot be blank.");
        }

        Value = value.Trim().ToUpperInvariant();
    }

    public string Value { get; }

    public override string ToString() => Value;
}
