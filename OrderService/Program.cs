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
builder.Services.AddScoped<OrderPublisher>();
builder.Services.AddHostedService<OrderUpdateConsumer>();


var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapPost("/order", async (OrderPublisher orderPublisher, OrderDbContext db) =>
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
    await db.SaveChangesAsync();

    var orderCreated = new OrderCreated(orderId, customerName);
    await orderPublisher.PublishOrderCreatedAsync(orderCreated);
    
    return Results.Ok(order);
});

app.MapGet("/orders", async (OrderDbContext db) => 
    await db.Orders.ToListAsync());

app.Run();