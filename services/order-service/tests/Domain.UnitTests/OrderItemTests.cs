using System.Reflection;
using Ecommerce.OrderService.Domain.Abstractions;
using Ecommerce.OrderService.Domain.Orders;

namespace Ecommerce.OrderService.Domain.UnitTests;

/// Guards PLAN.md section 18. If one of these fails, the test is right and the change is
/// wrong — an order item is a frozen record of what was agreed, not a view of a catalogue.
public class OrderItemTests
{
    [Test]
    public async Task An_order_item_offers_no_way_to_change_it()
    {
        // The real guard. Someone normalising OrderItem in month 4 has to add a setter
        // first, and this fails the moment they do.
        var settable = typeof(OrderItem)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.SetMethod is not null)
            .Select(property => property.Name)
            .ToList();

        await Assert.That(settable).IsEmpty();
    }

    [Test]
    public async Task A_later_price_rise_does_not_move_an_existing_order()
    {
        var order = OrderFixtures.AnOrder(OrderFixtures.AnItem(unitPrice: 18.99m));
        var totalWhenPlaced = order.Total;

        // The catalogue reprices the product. Nothing hands the order the new price,
        // and there is no code path that could apply it if something did.
        _ = Money.Of(25.00m, "USD");

        await Assert.That(order.Total).IsEqualTo(totalWhenPlaced);
        await Assert.That(order.Items[0].UnitPrice.Amount).IsEqualTo(18.99m);
    }

    [Test]
    public async Task An_order_item_keeps_the_name_the_customer_saw()
    {
        var item = OrderFixtures.AnItem();

        await Assert.That(item.ProductName).IsEqualTo("Cafetiere, 1 litre");
    }

    [Test]
    public async Task An_order_item_needs_a_quantity_of_at_least_one()
    {
        var zeroQuantity = () => OrderFixtures.AnItem(quantity: 0);

        await Assert.That(zeroQuantity).Throws<DomainException>();
    }

    [Test]
    public async Task A_line_charges_tax_on_the_whole_line_not_on_one_unit()
    {
        var item = OrderFixtures.AnItem(unitPrice: 18.99m, quantity: 2, taxBasisPoints: 1000);

        await Assert.That(item.Subtotal.Amount).IsEqualTo(37.98m);
        await Assert.That(item.Tax.Amount).IsEqualTo(3.80m);
        await Assert.That(item.Total.Amount).IsEqualTo(41.78m);
    }
}
