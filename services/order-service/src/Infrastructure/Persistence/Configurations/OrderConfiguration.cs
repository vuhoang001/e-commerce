using Ecommerce.OrderService.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.OrderService.Infrastructure.Persistence.Configurations;

/// Column names are spelled out rather than left to convention. This database gets opened
/// in psql during month 2's Debezium work, and quoted PascalCase identifiers are miserable
/// to type by hand.
public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders");

        builder.HasKey(order => order.Id);

        builder.Property(order => order.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => new OrderId(value))
            .ValueGeneratedNever();

        builder.Property(order => order.CustomerId)
            .HasColumnName("customer_id")
            .HasConversion(id => id.Value, value => new CustomerId(value));

        builder.Property(order => order.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(order => order.PlacedAt)
            .HasColumnName("placed_at");

        // Optimistic concurrency without a version column in the domain. Postgres already
        // stamps every row with xmin, the id of the transaction that last wrote it, so EF
        // can use that as the concurrency token. Two users editing one order means the
        // second SaveChanges throws DbUpdateConcurrencyException instead of silently
        // overwriting the first — and the domain never learns that any of this happened.
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // Computed from the lines on every read. Storing it would allow a total that
        // disagrees with the items it is supposed to sum.
        builder.Ignore(order => order.Total);
        builder.Ignore(order => order.DomainEvents);

        builder.OwnsOne(order => order.ShippingAddress, address =>
        {
            address.Property(a => a.Line1).HasColumnName("ship_line1").HasMaxLength(200).IsRequired();
            address.Property(a => a.Line2).HasColumnName("ship_line2").HasMaxLength(200);
            address.Property(a => a.City).HasColumnName("ship_city").HasMaxLength(100).IsRequired();
            address.Property(a => a.PostalCode).HasColumnName("ship_postal_code").HasMaxLength(20).IsRequired();
            address.Property(a => a.Country).HasColumnName("ship_country").HasMaxLength(2).IsRequired();
        });
        builder.Navigation(order => order.ShippingAddress).IsRequired();

        builder.OwnsMany(order => order.Items, item =>
        {
            item.ToTable("order_items");
            item.WithOwner().HasForeignKey("order_id");
            item.Property<int>("id");
            item.HasKey("id");

            item.Property(i => i.ProductId)
                .HasColumnName("product_id")
                .HasConversion(id => id.Value, value => new ProductId(value))
                .HasMaxLength(100);

            item.Property(i => i.ProductName).HasColumnName("product_name").HasMaxLength(400);

            item.Property(i => i.Sku)
                .HasColumnName("sku")
                .HasConversion(sku => sku.Value, value => new Sku(value))
                .HasMaxLength(64);

            // Two columns, because money without its currency is not money.
            item.OwnsOne(i => i.UnitPrice, price =>
            {
                price.Property(p => p.Amount).HasColumnName("unit_price_amount").HasPrecision(18, 4);
                price.Property(p => p.Currency).HasColumnName("unit_price_currency").HasMaxLength(3);
            });
            item.Navigation(i => i.UnitPrice).IsRequired();

            item.Property(i => i.TaxRate)
                .HasColumnName("tax_rate_basis_points")
                .HasConversion(rate => rate.BasisPoints, value => TaxRate.FromBasisPoints(value));

            item.Property(i => i.Quantity).HasColumnName("quantity");

            // Derived on read — never stored, so a stored line total can never drift from
            // the price and quantity it came from.
            item.Ignore(i => i.Subtotal);
            item.Ignore(i => i.Tax);
            item.Ignore(i => i.Total);
        });

        builder.Navigation(order => order.Items)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
