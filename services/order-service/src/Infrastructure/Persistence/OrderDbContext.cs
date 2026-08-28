using System.Reflection;
using Ecommerce.OrderService.Domain.Abstractions;
using Ecommerce.OrderService.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.OrderService.Infrastructure.Persistence;

/// The DbContext is the unit of work. Nothing else in the service gets to decide when a
/// transaction commits — the transaction behaviour calls SaveChangesAsync exactly once
/// per command.
public sealed class OrderDbContext(DbContextOptions<OrderDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public const string Schema = "ordering";

    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
