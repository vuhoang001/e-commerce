using Ecommerce.OrderService.Application.Orders.GetOrder;
using Ecommerce.OrderService.Application.Orders.PlaceOrder;
using Ecommerce.Rpc.Order.V1;
using Grpc.Core;
using MediatR;
using OrderServiceBase = Ecommerce.Rpc.Order.V1.OrderService.OrderServiceBase;
// The contract and the command both call a line a line. Aliasing here is clearer than
// renaming either — each name is right in its own layer.
using CommandLine = Ecommerce.OrderService.Application.Orders.PlaceOrder.PlaceOrderLine;

namespace Ecommerce.OrderService.Api.Orders;

/// Transport only. It translates a gRPC message into a request, sends it, and translates
/// the answer back — no business rules, no persistence, no decisions.
public sealed class OrderGrpcService(ISender sender) : OrderServiceBase
{
    public override async Task<GetOrderResponse> GetOrder(
        GetOrderRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.OrderId, out var orderId))
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument, $"'{request.OrderId}' is not an order id."));
        }

        var result = await sender.Send(new GetOrderQuery(orderId), context.CancellationToken);

        if (!result.IsSuccess)
        {
            // Absence travels as NOT_FOUND, never as an empty response — the contract says so.
            throw new RpcException(new Status(StatusCode.NotFound, result.Error.Message));
        }

        return new GetOrderResponse { Order = result.Value.ToProto() };
    }

    public override async Task<PlaceOrderResponse> PlaceOrder(
        PlaceOrderRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.CustomerId, out var customerId))
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument, $"'{request.CustomerId}' is not a customer id."));
        }

        var address = request.ShippingAddress ?? throw new RpcException(new Status(
            StatusCode.InvalidArgument, "An order needs a shipping address."));

        var command = new PlaceOrderCommand(
            customerId,
            new PlaceOrderAddress(address.Line1, address.Line2, address.City, address.PostalCode, address.Country),
            [.. request.Lines.Select(line => new CommandLine(
                line.ProductId,
                line.ProductName,
                line.Sku,
                line.UnitPrice.ToDomain().Amount,
                line.UnitPrice.CurrencyCode,
                line.TaxRateBasisPoints,
                line.Quantity))]);

        var result = await sender.Send(command, context.CancellationToken);

        return new PlaceOrderResponse { OrderId = result.Value.ToString() };
    }
}
