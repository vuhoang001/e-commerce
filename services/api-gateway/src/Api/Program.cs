using Ecommerce.ApiGateway.Api.Endpoints;
using OrderServiceClient = Ecommerce.Rpc.Order.V1.OrderService.OrderServiceClient;

var builder = WebApplication.CreateBuilder(args);

var orderServiceAddress = builder.Configuration["Services:OrderService"]
    ?? throw new InvalidOperationException(
        "Services:OrderService is not configured. The gateway cannot start without knowing where order-service is.");

builder.Services
    .AddGrpcClient<OrderServiceClient>(options => options.Address = new Uri(orderServiceAddress));

var app = builder.Build();

app.MapOrderEndpoints();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
