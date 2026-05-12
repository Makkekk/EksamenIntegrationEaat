using System.Text;
using System.Text.Json;
using Contracts;
using CourierService.Data;
using CourierService.Messaging;
using CourierService.Models;
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
builder.Services.AddSingleton<CourierPublisher>();
builder.Services.AddHostedService<CourierConsumer>();
builder.Services.AddHostedService<OutboxProcessor>();

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
    
    // Opret Outbox besked for tildeling
    var assigned = new CourierAssigned(orderId, Guid.NewGuid(), name);
    db.OutboxMessages.Add(new OutboxMessage
    {
        Id = Guid.NewGuid(),
        Type = "CourierAssigned",
        RoutingKey = "courier.assigned",
        Content = JsonSerializer.Serialize(assigned),
        CreatedAt = DateTime.UtcNow
    });

    // Opret Outbox besked for broadcast (så andre bud ser den er taget)
    var taskTaken = new { orderId = orderId, status = "Taken" };
    db.OutboxMessages.Add(new OutboxMessage
    {
        Id = Guid.NewGuid(),
        Type = "CourierBroadcastTaken",
        RoutingKey = "courier.broadcast.taken",
        Content = JsonSerializer.Serialize(taskTaken),
        CreatedAt = DateTime.UtcNow
    });

    await db.SaveChangesAsync();

    return Results.Ok($"Success! Du har fået opgaven #{orderId}.");
});

app.Run();