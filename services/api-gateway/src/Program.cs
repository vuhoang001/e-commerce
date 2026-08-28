using Ecommerce.ApiGateway.Clients;
using Ecommerce.ApiGateway.Orders;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpcClients(builder.Configuration);

var app = builder.Build();

app.MapOrderEndpoints();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
