using System.Text;
using System.Text.Json;
using Contracts;
using CourierService.Data;
using CourierService.Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder();

// In-Memory Database setup
builder.Services.AddDbContext<CourierDbContext>(options =>
    options.UseInMemoryDatabase("CourierDb"));

builder.Services.AddOpenApi();
builder.Services.AddHostedService<CourierConsumer>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// Endpoint til at se ledige ordrer
app.MapGet("/offers", async (CourierDbContext db) =>
    await db.DeliveryOffers.Where(o => !o.IsAssigned).ToListAsync());

// Endpoint til at acceptere en ordre
app.MapPost("/accept/{orderId}/{name}", async (Guid orderId, string name, CourierDbContext db) =>
{
    var offer = await db.DeliveryOffers.FindAsync(orderId);

    if (offer == null || offer.IsAssigned)
        return Results.BadRequest("Opgaven er ikke længere ledig.");
    
    

    // Gem tildeling (Først-til-mølle)
    offer.IsAssigned = true;
    offer.CourierName = name;
    await db.SaveChangesAsync();

    // Send besked videre til systemet
    var factory = new ConnectionFactory { HostName = "localhost" };
    var connection = await factory.CreateConnectionAsync();
    var channel = await connection.CreateChannelAsync();

    var assigned = new CourierAssigned(orderId, Guid.NewGuid(), name);
    var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(assigned));

    await channel.BasicPublishAsync("eaat_exchange", "courier.assigned", body);
    
    var taskTaken = new { orderId = orderId, status = "Taken" };
    var taskTakenBody = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(taskTaken));
    
    await channel.BasicPublishAsync(
        exchange: "eaat_exchange",
        routingKey: "delivery.broadcast.taken",
        body: taskTakenBody);

    return Results.Ok($"Success! Du har fået opgaven #{orderId}.");

    
});

app.Run();