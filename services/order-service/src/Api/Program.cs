using Ecommerce.OrderService.Api.Grpc;
using Ecommerce.OrderService.Api.Orders;
using Ecommerce.OrderService.Application;
using Ecommerce.OrderService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddGrpc(options => options.Interceptors.Add<ErrorInterceptor>());

var app = builder.Build();

app.MapGrpcService<OrderGrpcService>();

app.Run();
