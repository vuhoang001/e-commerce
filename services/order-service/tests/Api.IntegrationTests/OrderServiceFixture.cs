using Ecommerce.OrderService.Infrastructure.Persistence;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TUnit.Core.Interfaces;
using Testcontainers.PostgreSql;

namespace Ecommerce.OrderService.Api.IntegrationTests;

/// Boots order-service against a throwaway Postgres.
///
/// One container for the whole assembly, not one per test: starting Postgres costs a
/// couple of seconds and the tests do not interfere with each other — each places its own
/// order and only looks at that one.
public sealed class OrderServiceFixture : WebApplicationFactory<Program>, IAsyncInitializer
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        // The same image compose.infra.yml runs. A test passing against a different major
        // version than production proves less than it appears to.
        .WithImage("postgres:17-alpine")
        .WithDatabase("ordering")
        .Build();

    public GrpcChannel Channel { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        // Applies the real migrations, so the hand-edited one that must not create the
        // xmin column is exercised on every run rather than trusted.
        using (var scope = Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<OrderDbContext>().Database.MigrateAsync();
        }

        Channel = GrpcChannel.ForAddress(
            ClientOptions.BaseAddress,
            new GrpcChannelOptions { HttpHandler = Server.CreateHandler() });
    }

    /// Resolves a scoped service, for the tests that need to go behind the API and look at
    /// the database directly.
    public T Resolve<T>(IServiceScope scope) where T : notnull =>
        scope.ServiceProvider.GetRequiredService<T>();

    public IServiceScope NewScope() => Services.CreateScope();

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting("ConnectionStrings:OrderDatabase", _postgres.GetConnectionString());

    public override async ValueTask DisposeAsync()
    {
        Channel?.Dispose();
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
