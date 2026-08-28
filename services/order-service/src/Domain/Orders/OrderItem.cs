using Ecommerce.OrderService.Domain.Abstractions;

namespace Ecommerce.OrderService.Domain.Orders;

/// A line on an order — a frozen copy of what was bought, at the price agreed.
///
/// This is a `record` with no setters on purpose. PLAN.md section 18 says an order item
/// snapshots product data and is never updated again; making it immutable means the
/// compiler enforces that rather than a convention nobody remembers. When the catalogue
/// later renames the product, raises its price or delists it entirely, this line does not
/// move — an order is a financial record of an agreement, not a live view of a catalogue.
///
/// Note what is NOT copied: category, brand and supplier. Those are analytical dimensions
/// that Flink joins onto the event stream (PLAN.md section 18); they were never part of
/// what the customer agreed to.
public sealed record OrderItem
{
    private OrderItem(
        ProductId productId,
        string productName,
        Sku sku,
        Money unitPrice,
        TaxRate taxRate,
        int quantity)
    {
        ProductId = productId;
        ProductName = productName;
        Sku = sku;
        UnitPrice = unitPrice;
        TaxRate = taxRate;
        Quantity = quantity;
    }

    /// The link back to the catalogue, for support and analytics. Never resolved to read a
    /// name or a price — the snapshot below is the authority.
    public ProductId ProductId { get; }

    public string ProductName { get; }
    public Sku Sku { get; }
    public Money UnitPrice { get; }
    public TaxRate TaxRate { get; }
    public int Quantity { get; }

    public Money Subtotal => UnitPrice * Quantity;

    public Money Tax => TaxRate.ApplyTo(Subtotal);

    public Money Total => Subtotal + Tax;

    /// Named for what it does. `Create` would suggest a line that might later be refreshed;
    /// this takes a copy of the catalogue as it is right now and keeps it forever.
    public static OrderItem Snapshot(
        ProductId productId,
        string productName,
        Sku sku,
        Money unitPrice,
        TaxRate taxRate,
        int quantity)
    {
        if (string.IsNullOrWhiteSpace(productName))
        {
            throw new DomainException("An order item needs the product name as the customer saw it.");
        }

        if (quantity <= 0)
        {
            throw new DomainException($"An order item needs a quantity of at least 1, but was {quantity}.");
        }

        return new OrderItem(productId, productName.Trim(), sku, unitPrice, taxRate, quantity);
    }
}
