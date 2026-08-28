using Ecommerce.OrderService.Domain.Abstractions;
using Ecommerce.OrderService.Domain.Orders;
using Ecommerce.OrderService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.OrderService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("OrderDatabase")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:OrderDatabase is not configured. order-service cannot start without a database.");

        services.AddDbContext<OrderDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<OrderDbContext>());

        return services;
    }
}
