using Ecommerce.Rpc.Order.V1;
using Grpc.Core;
using OrderServiceClient = Ecommerce.Rpc.Order.V1.OrderService.OrderServiceClient;

namespace Ecommerce.ApiGateway.Orders;

internal static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        // {id} is not constrained to a guid: order_id is a string in the contract, and
        // the walking skeleton's seeded order is "1". Constrain it when the domain
        // decides what an order id actually is, in month 1.
        app.MapGet("/api/orders/{id}", async (
            string id,
            OrderServiceClient orders,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var reply = await orders.GetOrderAsync(
                    new GetOrderRequest { OrderId = id },
                    cancellationToken: cancellationToken);

                return Results.Ok(reply.Order.ToResponse());
            }
            catch (RpcException e) when (e.StatusCode == StatusCode.NotFound)
            {
                // Translate the gRPC status into the HTTP status a browser understands.
                // Letting it escape would surface as 500 and imply the gateway broke.
                return Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Order not found",
                    detail: e.Status.Detail);
            }
            catch (RpcException e) when (e.StatusCode == StatusCode.Unavailable)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "order-service is unreachable",
                    detail: e.Status.Detail);
            }
        })
        .WithName("GetOrder");

        return app;
    }
}
