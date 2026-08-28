using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ecommerce.OrderService.Infrastructure.Persistence;

/// Used only by `dotnet ef` at design time. Without it the tooling has to boot the Api
/// project to find a DbContext, which drags a running configuration into what should be a
/// pure schema operation.
///
/// The connection string here is never used to talk to a real database — migrations are
/// generated from the model, not from the server.
public sealed class OrderDbContextFactory : IDesignTimeDbContextFactory<OrderDbContext>
{
    public OrderDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ORDER_DATABASE")
            ?? "Host=localhost;Port=5432;Database=ordering;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<OrderDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new OrderDbContext(options);
    }
}
