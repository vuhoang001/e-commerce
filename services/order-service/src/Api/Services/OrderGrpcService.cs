using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Ecommerce.Common.V1;
using Ecommerce.Rpc.Order.V1;
using OrderServiceBase = Ecommerce.Rpc.Order.V1.OrderService.OrderServiceBase;

namespace Ecommerce.OrderService.Api.Services;

/// Month 0.5 walking skeleton: proves the contract, the generated stubs and the host
/// fit together. Every value below is hard-coded on purpose. The Order aggregate and
/// a real repository arrive in month 1 (PLAN.md section 8); until then there is no
/// domain logic here to get wrong.
public sealed class OrderGrpcService : OrderServiceBase
{
    private static readonly Order SeedOrder = new()
    {
        OrderId = "1",
        CustomerId = "customer-1",
        Status = OrderStatus.Confirmed,
        PlacedAt = Timestamp.FromDateTimeOffset(
            new DateTimeOffset(2026, 8, 28, 9, 30, 0, TimeSpan.Zero)),
        // 18.99 x 2 = 37.98, plus 10% tax of 3.80, is 41.78. The first version of this
        // seed said 41.97, which matches nothing — see ADR-005. Once the Order aggregate
        // computes the total this class disappears and the arithmetic cannot drift again.
        Total = new Money { CurrencyCode = "USD", Units = 41, Nanos = 780_000_000 },
        Items =
        {
            new OrderItem
            {
                ProductId = "product-7",
                ProductName = "Cafetiere, 1 litre",
                Sku = "KIT-CAF-1L",
                UnitPrice = new Money { CurrencyCode = "USD", Units = 18, Nanos = 990_000_000 },
                TaxRateBasisPoints = 1000,
                Quantity = 2,
            },
        },
    };

    public override Task<GetOrderResponse> GetOrder(
        GetOrderRequest request,
        ServerCallContext context)
    {
        if (request.OrderId != SeedOrder.OrderId)
        {
            // Absence is NOT_FOUND, never an empty response — the contract says so.
            throw new RpcException(new Status(
                StatusCode.NotFound,
                $"No order with id '{request.OrderId}'."));
        }

        return Task.FromResult(new GetOrderResponse { Order = SeedOrder });
    }
}
