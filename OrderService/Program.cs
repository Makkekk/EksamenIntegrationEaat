using Contracts;
using OrderService.Data;
using OrderService.Messaging;
using OrderService.Models;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseInMemoryDatabase("OrderDb"));

builder.Services.AddOpenApi();
builder.Services.AddSingleton<OrderPublisher>();
builder.Services.AddHostedService<OrderUpdateConsumer>();
builder.Services.AddHostedService<OutboxProcessor>();


var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapPost("/order", async (OrderDbContext db) =>
{
    var orderId = Guid.NewGuid();
    var customerName = "Søren Sørensen";
    
    var order = new Order
    {
        OrderId = orderId,
        CustomerName = customerName,
        Status = "Created"
    };

    
    db.Orders.Add(order);
    
    var orderCreatedEvent = new OrderCreated(orderId, customerName);
    var outboxMessage = new OutboxMessage
    {
        Id = Guid.NewGuid(),
        Type = "OrderCreated",
        Content = System.Text.Json.JsonSerializer.Serialize(orderCreatedEvent),
        CreatedAt = DateTime.UtcNow
    };
    db.OutboxMessages.Add(outboxMessage);
    
    await db.SaveChangesAsync();

    return Results.Ok(order);
});

app.MapGet("/orders", async (OrderDbContext db) => 
    await db.Orders.ToListAsync());

app.Run();