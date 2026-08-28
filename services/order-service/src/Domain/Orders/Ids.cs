using Ecommerce.OrderService.Domain.Abstractions;

namespace Ecommerce.OrderService.Domain.Orders;

/// Strongly typed so that an order id cannot be passed where a customer id is expected.
/// Both are Guids at rest; the compiler is what stops them being swapped.
public readonly record struct OrderId(Guid Value)
{
    public static OrderId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}

public readonly record struct CustomerId(Guid Value)
{
    public override string ToString() => Value.ToString();
}

/// A string, not a Guid: product ids are assigned by the catalogue, which is a separate
/// service with its own conventions. This is the one identifier this service does not own.
///
/// A record class for the same reason as Sku: a struct's default value would be a null
/// string that never passed through the constructor below.
public sealed record ProductId
{
    public ProductId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("A product id cannot be blank.");
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
