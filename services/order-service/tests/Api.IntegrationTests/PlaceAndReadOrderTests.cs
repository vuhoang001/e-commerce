using Ecommerce.OrderService.Domain.Orders;
using Ecommerce.OrderService.Infrastructure.Persistence;
using Ecommerce.Rpc.Order.V1;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using OrderServiceClient = Ecommerce.Rpc.Order.V1.OrderService.OrderServiceClient;
// Both the domain and the contract call it OrderStatus, and this project sees both.
using ProtoStatus = Ecommerce.Rpc.Order.V1.OrderStatus;

namespace Ecommerce.OrderService.Api.IntegrationTests;

[ClassDataSource<OrderServiceFixture>(Shared = SharedType.PerAssembly)]
public class PlaceAndReadOrderTests(OrderServiceFixture fixture)
{
    private OrderServiceClient Orders => new(fixture.Channel);

    [Test]
    public async Task An_order_survives_the_round_trip_through_Postgres()
    {
        // The mapping is the fragile part: value converters for the SKU and the product id,
        // an owned collection for the lines, an owned type inside it for the price. All of
        // that compiles whether or not it is right.
        var placed = await Orders.PlaceOrderAsync(Requests.AnOrder());

        var read = await Orders.GetOrderAsync(new GetOrderRequest { OrderId = placed.OrderId });
        var order = read.Order;
        var line = order.Items.Single();

        await Assert.That(order.Status).IsEqualTo(ProtoStatus.Pending);
        await Assert.That(line.Sku).IsEqualTo("KIT-CAF-1L");
        await Assert.That(line.ProductName).IsEqualTo("Cafetiere, 1 litre");
        await Assert.That(line.ProductId).IsEqualTo("product-7");
        await Assert.That(line.Quantity).IsEqualTo(2);
        await Assert.That(line.TaxRateBasisPoints).IsEqualTo(1000);
        await Assert.That(line.UnitPrice.CurrencyCode).IsEqualTo("USD");
        await Assert.That(line.UnitPrice.Units).IsEqualTo(18);
        await Assert.That(line.UnitPrice.Nanos).IsEqualTo(990_000_000);
    }

    [Test]
    public async Task The_total_is_computed_from_the_lines_not_read_from_a_column()
    {
        // 18.99 x 2 = 37.98, plus 10% tax of 3.80, is 41.78. There is no total column: if
        // this number is right after a round trip, it was recomputed from stored lines.
        var placed = await Orders.PlaceOrderAsync(Requests.AnOrder());

        var read = await Orders.GetOrderAsync(new GetOrderRequest { OrderId = placed.OrderId });

        await Assert.That(read.Order.Total.Units).IsEqualTo(41);
        await Assert.That(read.Order.Total.Nanos).IsEqualTo(780_000_000);
        await Assert.That(read.Order.Total.CurrencyCode).IsEqualTo("USD");
    }

    [Test]
    public async Task The_shipping_address_is_stored_as_columns_on_the_order()
    {
        var placed = await Orders.PlaceOrderAsync(Requests.AnOrder());

        using var scope = fixture.NewScope();
        var context = fixture.Resolve<OrderDbContext>(scope);
        var stored = await context.Orders
            .FirstAsync(order => order.Id == new OrderId(Guid.Parse(placed.OrderId)));

        await Assert.That(stored.ShippingAddress.City).IsEqualTo("London");
        await Assert.That(stored.ShippingAddress.Country).IsEqualTo("GB");
        await Assert.That(stored.ShippingAddress.Line2).IsNull();
    }

    [Test]
    public async Task The_command_is_committed_by_the_pipeline_not_by_the_handler()
    {
        // PlaceOrderCommandHandler never calls SaveChanges. If the row is here, the
        // transaction behaviour committed it — which is the only thing keeping
        // "one aggregate per transaction" true rather than merely intended.
        var placed = await Orders.PlaceOrderAsync(Requests.AnOrder());

        using var scope = fixture.NewScope();
        var context = fixture.Resolve<OrderDbContext>(scope);
        var exists = await context.Orders
            .AnyAsync(order => order.Id == new OrderId(Guid.Parse(placed.OrderId)));

        await Assert.That(exists).IsTrue();
    }

    [Test]
    public async Task An_unknown_order_is_not_found()
    {
        var call = async () => await Orders.GetOrderAsync(
            new GetOrderRequest { OrderId = Guid.NewGuid().ToString() });

        var exception = await Assert.ThrowsAsync<RpcException>(call);
        await Assert.That(exception!.StatusCode).IsEqualTo(StatusCode.NotFound);
    }

    [Test]
    public async Task A_request_with_no_lines_is_rejected_before_the_domain_sees_it()
    {
        var request = Requests.AnOrder();
        request.Lines.Clear();

        var call = async () => await Orders.PlaceOrderAsync(request);

        var exception = await Assert.ThrowsAsync<RpcException>(call);
        await Assert.That(exception!.StatusCode).IsEqualTo(StatusCode.InvalidArgument);
    }

    [Test]
    public async Task A_request_the_domain_refuses_is_a_failed_precondition_not_an_invalid_argument()
    {
        // Well formed, so validation passes; the aggregate is the one that says no. The two
        // rejections have to be distinguishable, or a caller cannot tell a bug in its
        // request from a rule it did not know about.
        var call = async () => await Orders.PlaceOrderAsync(Requests.AnOrder(currency: "DOLLAR"));

        var exception = await Assert.ThrowsAsync<RpcException>(call);
        await Assert.That(exception!.StatusCode).IsEqualTo(StatusCode.FailedPrecondition);
    }
}
