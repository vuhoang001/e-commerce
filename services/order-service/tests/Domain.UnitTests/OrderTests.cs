using Ecommerce.OrderService.Domain.Abstractions;
using Ecommerce.OrderService.Domain.Orders;
using Ecommerce.OrderService.Domain.Orders.Events;

namespace Ecommerce.OrderService.Domain.UnitTests;

public class OrderTests
{
    [Test]
    public async Task An_order_needs_at_least_one_item()
    {
        var place = () => Order.Place(
            OrderId.New(), new CustomerId(Guid.NewGuid()), OrderFixtures.AnAddress(), [], OrderFixtures.Now);

        await Assert.That(place).Throws<DomainException>();
    }

    [Test]
    public async Task An_order_is_in_a_single_currency()
    {
        var place = () => Order.Place(
            OrderId.New(),
            new CustomerId(Guid.NewGuid()),
            OrderFixtures.AnAddress(),
            [OrderFixtures.AnItem(currency: "USD"), OrderFixtures.AnItem(currency: "EUR")],
            OrderFixtures.Now);

        await Assert.That(place).Throws<DomainException>();
    }

    [Test]
    public async Task A_placed_order_starts_pending_and_announces_itself()
    {
        var order = OrderFixtures.AnOrder();

        await Assert.That(order.Status).IsEqualTo(OrderStatus.Pending);
        await Assert.That(order.DomainEvents.Count).IsEqualTo(1);
        await Assert.That(order.DomainEvents[0]).IsTypeOf<OrderPlaced>();
    }

    [Test]
    public async Task The_total_is_the_sum_of_its_lines_including_tax()
    {
        // 18.99 x 2 = 37.98 subtotal, 10% tax = 3.798 rounded to 3.80, total 41.78.
        var order = OrderFixtures.AnOrder(OrderFixtures.AnItem());

        await Assert.That(order.Total.Amount).IsEqualTo(41.78m);
        await Assert.That(order.Total.Currency).IsEqualTo("USD");
    }

    [Test]
    public async Task Confirming_a_pending_order_moves_it_to_confirmed()
    {
        var order = OrderFixtures.AnOrder();

        order.Confirm(OrderFixtures.Now);

        await Assert.That(order.Status).IsEqualTo(OrderStatus.Confirmed);
        await Assert.That(order.DomainEvents.Any(e => e is OrderConfirmed)).IsTrue();
    }

    [Test]
    public async Task An_order_cannot_be_confirmed_twice()
    {
        var order = OrderFixtures.AnOrder();
        order.Confirm(OrderFixtures.Now);

        var confirmAgain = () => order.Confirm(OrderFixtures.Now);

        await Assert.That(confirmAgain).Throws<DomainException>();
    }

    [Test]
    public async Task Cancelling_needs_a_reason()
    {
        var order = OrderFixtures.AnOrder();

        var cancelWithoutReason = () => order.Cancel("  ", OrderFixtures.Now);

        await Assert.That(cancelWithoutReason).Throws<DomainException>();
    }

    [Test]
    public async Task A_cancelled_order_cannot_be_cancelled_again()
    {
        var order = OrderFixtures.AnOrder();
        order.Cancel("Customer changed their mind", OrderFixtures.Now);

        var cancelAgain = () => order.Cancel("Again", OrderFixtures.Now);

        await Assert.That(cancelAgain).Throws<DomainException>();
    }

    [Test]
    public async Task A_confirmed_order_can_still_be_cancelled()
    {
        var order = OrderFixtures.AnOrder();
        order.Confirm(OrderFixtures.Now);

        order.Cancel("Out of stock after all", OrderFixtures.Now);

        await Assert.That(order.Status).IsEqualTo(OrderStatus.Cancelled);
    }

    [Test]
    public async Task Items_cannot_be_added_by_reaching_past_the_aggregate()
    {
        var order = OrderFixtures.AnOrder();

        await Assert.That(order.Items).IsAssignableTo<System.Collections.ObjectModel.ReadOnlyCollection<OrderItem>>();
        await Assert.That(order.Items.Count).IsEqualTo(1);
    }
}
