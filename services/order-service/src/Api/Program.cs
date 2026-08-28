using Ecommerce.OrderService.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();

var app = builder.Build();

app.MapGrpcService<OrderGrpcService>();

app.Run();
