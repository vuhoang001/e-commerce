using OrderServiceClient = Ecommerce.Rpc.Order.V1.OrderService.OrderServiceClient;

namespace Ecommerce.ApiGateway.Clients;

/// Registration for every gRPC client the gateway holds. Technical rather than
/// feature-shaped on purpose: addresses, and from month 2 the Polly timeout, jittered
/// retry and circuit breaker, apply to every service call regardless of which feature
/// makes it.
internal static class GrpcClients
{
    public static IServiceCollection AddGrpcClients(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddGrpcClient<OrderServiceClient>(
            options => options.Address = AddressOf(configuration, "OrderService"));

        return services;
    }

    /// Fails at startup rather than on the first request. A gateway that starts without
    /// knowing where a service lives only reports the problem once a user hits it.
    private static Uri AddressOf(IConfiguration configuration, string serviceName)
    {
        var address = configuration[$"Services:{serviceName}"]
            ?? throw new InvalidOperationException(
                $"Services:{serviceName} is not configured. The gateway cannot start without knowing where {serviceName} is.");

        return new Uri(address);
    }
}
