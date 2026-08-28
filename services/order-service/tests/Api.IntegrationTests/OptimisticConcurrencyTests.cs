using Ecommerce.OrderService.Domain.Orders;
using Ecommerce.OrderService.Infrastructure.Persistence;
using Ecommerce.Rpc.Order.V1;
using Microsoft.EntityFrameworkCore;
using OrderServiceClient = Ecommerce.Rpc.Order.V1.OrderService.OrderServiceClient;
// These assertions are about the stored aggregate, so they want the domain's enum.
using DomainStatus = Ecommerce.OrderService.Domain.Orders.OrderStatus;

namespace Ecommerce.OrderService.Api.IntegrationTests;

/// Guards ADR-006. There is no version column anywhere in the schema — the concurrency
/// token is Postgres's own xmin, mapped as a shadow property. Nothing in the domain, the
/// model or the compiler would notice if that mapping were quietly removed; only this does.
[ClassDataSource<OrderServiceFixture>(Shared = SharedType.PerAssembly)]
public class OptimisticConcurrencyTests(OrderServiceFixture fixture)
{
    [Test]
    public async Task The_second_of_two_concurrent_writers_is_rejected()
    {
        var placed = await new OrderServiceClient(fixture.Channel).PlaceOrderAsync(Requests.AnOrder());
        var id = new OrderId(Guid.Parse(placed.OrderId));

        // Two people open the same order. Neither knows about the other.
        using var aliceScope = fixture.NewScope();
        using var bobScope = fixture.NewScope();
        var aliceContext = fixture.Resolve<OrderDbContext>(aliceScope);
        var bobContext = fixture.Resolve<OrderDbContext>(bobScope);

        var aliceOrder = await aliceContext.Orders.FirstAsync(order => order.Id == id);
        var bobOrder = await bobContext.Orders.FirstAsync(order => order.Id == id);

        aliceOrder.Confirm(DateTimeOffset.UtcNow);
        await aliceContext.SaveChangesAsync();

        bobOrder.Cancel("Bob was not paying attention", DateTimeOffset.UtcNow);
        var bobSaves = async () => await bobContext.SaveChangesAsync();

        // Without the token this would succeed and Alice's confirmation would vanish with
        // no error anywhere. That silence is the bug this exists to prevent.
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(bobSaves);
    }

    [Test]
    public async Task The_first_writer_wins_and_the_second_changes_nothing()
    {
        var placed = await new OrderServiceClient(fixture.Channel).PlaceOrderAsync(Requests.AnOrder());
        var id = new OrderId(Guid.Parse(placed.OrderId));

        using (var aliceScope = fixture.NewScope())
        using (var bobScope = fixture.NewScope())
        {
            var aliceContext = fixture.Resolve<OrderDbContext>(aliceScope);
            var bobContext = fixture.Resolve<OrderDbContext>(bobScope);

            var aliceOrder = await aliceContext.Orders.FirstAsync(order => order.Id == id);
            var bobOrder = await bobContext.Orders.FirstAsync(order => order.Id == id);

            aliceOrder.Confirm(DateTimeOffset.UtcNow);
            await aliceContext.SaveChangesAsync();

            bobOrder.Cancel("Losing writer", DateTimeOffset.UtcNow);
            try
            {
                await bobContext.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                // Expected. The point of this test is what the database holds afterwards.
            }
        }

        using var checkScope = fixture.NewScope();
        var stored = await fixture.Resolve<OrderDbContext>(checkScope).Orders
            .FirstAsync(order => order.Id == id);

        await Assert.That(stored.Status).IsEqualTo(DomainStatus.Confirmed);
    }

    [Test]
    public async Task A_write_that_nobody_raced_still_succeeds()
    {
        // The other half of the guarantee. A concurrency token that rejected every second
        // write would also pass the tests above, and would be useless.
        var placed = await new OrderServiceClient(fixture.Channel).PlaceOrderAsync(Requests.AnOrder());
        var id = new OrderId(Guid.Parse(placed.OrderId));

        using (var scope = fixture.NewScope())
        {
            var context = fixture.Resolve<OrderDbContext>(scope);
            var order = await context.Orders.FirstAsync(o => o.Id == id);
            order.Confirm(DateTimeOffset.UtcNow);
            await context.SaveChangesAsync();

            order.Cancel("Changed our minds, uncontested", DateTimeOffset.UtcNow);
            await context.SaveChangesAsync();
        }

        using var checkScope = fixture.NewScope();
        var stored = await fixture.Resolve<OrderDbContext>(checkScope).Orders
            .FirstAsync(order => order.Id == id);

        await Assert.That(stored.Status).IsEqualTo(DomainStatus.Cancelled);
    }
}
