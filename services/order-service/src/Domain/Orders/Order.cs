using Ecommerce.OrderService.Domain.Abstractions;
using Ecommerce.OrderService.Domain.Orders.Events;

namespace Ecommerce.OrderService.Domain.Orders;

/// The order aggregate. Everything that changes together when an order changes lives
/// behind this type, and nothing outside reaches past it to touch a line directly.
public sealed class Order : AggregateRoot<OrderId>
{
    private readonly List<OrderItem> _items = [];

    private Order(
        OrderId id,
        CustomerId customerId,
        Address shippingAddress,
        IEnumerable<OrderItem> items,
        DateTimeOffset placedAt)
        : base(id)
    {
        CustomerId = customerId;
        ShippingAddress = shippingAddress;
        PlacedAt = placedAt;
        Status = OrderStatus.Pending;
        _items.AddRange(items);
    }

    /// Only for the persistence layer's materialiser.
    private Order() { }

    public CustomerId CustomerId { get; private set; }

    public OrderStatus Status { get; private set; }

    public Address ShippingAddress { get; private set; } = null!;

    public DateTimeOffset PlacedAt { get; private set; }

    /// A copy. Handing out the live list would let a caller add a line without going
    /// through the aggregate, which is exactly what the aggregate exists to prevent.
    public IReadOnlyList<OrderItem> Items => _items.AsReadOnly();

    /// Computed, never stored as an independently settable value. A total that can be set
    /// is a total that can disagree with its own lines.
    public Money Total =>
        _items.Aggregate(Money.Zero(_items[0].UnitPrice.Currency), (running, item) => running + item.Total);

    /// Named for the intent, not the mechanism. `new Order()` says nothing about what
    /// placing an order means or which rules it has to satisfy.
    public static Order Place(
        OrderId id,
        CustomerId customerId,
        Address shippingAddress,
        IReadOnlyCollection<OrderItem> items,
        DateTimeOffset placedAt)
    {
        ArgumentNullException.ThrowIfNull(shippingAddress);
        ArgumentNullException.ThrowIfNull(items);

        if (items.Count == 0)
        {
            throw new DomainException("An order needs at least one item.");
        }

        var currencies = items.Select(item => item.UnitPrice.Currency).Distinct().ToList();
        if (currencies.Count > 1)
        {
            throw new DomainException(
                $"An order is in one currency, but these items are in {string.Join(" and ", currencies)}.");
        }

        var order = new Order(id, customerId, shippingAddress, items, placedAt);
        order.Raise(new OrderPlaced(id, customerId, order.Total, placedAt));

        return order;
    }

    public void Confirm(DateTimeOffset at)
    {
        if (Status != OrderStatus.Pending)
        {
            throw new DomainException($"Only a pending order can be confirmed; this one is {Status}.");
        }

        Status = OrderStatus.Confirmed;
        Raise(new OrderConfirmed(Id, at));
    }

    public void Cancel(string reason, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("Cancelling an order needs a reason — someone will ask why in six months.");
        }

        if (Status == OrderStatus.Cancelled)
        {
            throw new DomainException("This order is already cancelled.");
        }

        Status = OrderStatus.Cancelled;
        Raise(new OrderCancelled(Id, reason.Trim(), at));
    }
}
