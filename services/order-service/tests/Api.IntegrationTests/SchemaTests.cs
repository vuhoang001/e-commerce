using Ecommerce.OrderService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using OrderServiceClient = Ecommerce.Rpc.Order.V1.OrderService.OrderServiceClient;

namespace Ecommerce.OrderService.Api.IntegrationTests;

/// The migration is checked-in code with a line deliberately deleted from it. These prove
/// the edit is still correct against a real server, which is the only place it can be.
[ClassDataSource<OrderServiceFixture>(Shared = SharedType.PerAssembly)]
public class SchemaTests(OrderServiceFixture fixture)
{
    [Test]
    public async Task The_migrations_leave_nothing_pending()
    {
        // Catches the usual drift: a model change made without `make migration`, which
        // works locally against an already-correct database and fails on a fresh one.
        using var scope = fixture.NewScope();
        var context = fixture.Resolve<OrderDbContext>(scope);

        var pending = await context.Database.GetPendingMigrationsAsync();

        await Assert.That(pending).IsEmpty();
    }

    [Test]
    public async Task There_is_no_xmin_column_because_Postgres_already_provides_one()
    {
        // The scaffolded migration tried to CREATE this column, which cannot work: xmin is
        // a system column on every table. If someone regenerates the migration and forgets
        // to remove that line again, the migration fails and the assembly never gets here —
        // but if it were merged as an ordinary column, the token would silently stop being
        // the transaction id and start being a value nobody updates.
        // Place one first: this test asserts something about a row, and nothing guarantees
        // another test class has run. A test that depends on the order tests running first
        // is a test that fails at random.
        await new OrderServiceClient(fixture.Channel).PlaceOrderAsync(Requests.AnOrder());

        using var scope = fixture.NewScope();
        var context = fixture.Resolve<OrderDbContext>(scope);

        var userColumns = await context.Database
            .SqlQuery<string>($"""
                select column_name
                from information_schema.columns
                where table_schema = 'ordering' and table_name = 'orders'
                """)
            .ToListAsync();

        await Assert.That(userColumns).DoesNotContain("xmin");

        // ...and it is nevertheless readable, which is the whole point. Cast to text in
        // SQL: xid is a Postgres internal type that the ADO layer will not hand back as a
        // number, and the value here only has to be present, not arithmetic.
        var systemColumn = await context.Database
            .SqlQuery<string>($"select xmin::text from ordering.orders limit 1")
            .ToListAsync();

        await Assert.That(systemColumn).IsNotEmpty();
    }
}
