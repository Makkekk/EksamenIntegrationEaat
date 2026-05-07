using Contracts;
using OrderService.Messaging;
using Scalar.AspNetCore;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddScoped<OrderPublisher>();
builder.Services.AddHostedService<OrderUpdateConsumer>();


var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapPost("/order", async (OrderPublisher orderPublisher) =>
{
    var order = new OrderCreated(Guid.NewGuid(), "Søren Sørensen");
    
    await orderPublisher.PublishOrderCreatedAsync(order);
    
    return Results.Ok(order);
});

app.Run();