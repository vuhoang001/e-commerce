using System.Reflection;

namespace Ecommerce.ArchitectureTests;

internal static class Layers
{
    public static readonly Assembly Domain =
        typeof(OrderService.Domain.Orders.Order).Assembly;

    public static readonly Assembly Application =
        typeof(OrderService.Application.DependencyInjection).Assembly;

    public static readonly Assembly Infrastructure =
        typeof(OrderService.Infrastructure.DependencyInjection).Assembly;

    public static readonly Assembly Api =
        typeof(OrderService.Api.Orders.OrderGrpcService).Assembly;

    public static readonly Assembly Gateway =
        typeof(ApiGateway.Orders.OrderResponse).Assembly;

    public static IEnumerable<string> ReferencesOf(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(reference => reference.Name!);
}
