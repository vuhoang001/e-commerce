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

/// Top-level statements compile into an internal Program class, which
/// WebApplicationFactory cannot reach. Making it public is the documented way to let the
/// integration tests boot this exact host rather than a reconstruction of it.
public partial class Program;
