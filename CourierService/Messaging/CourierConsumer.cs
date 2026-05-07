using System.Text;
using System.Text.Json;
using Contracts;
using CourierService.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CourierService.Messaging;

public class CourierConsumer : BackgroundService
{
    private readonly string hostname = "localhost";
    private readonly IServiceScopeFactory _scopeFactory;

    public CourierConsumer(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory { HostName = hostname };
        var connection = await factory.CreateConnectionAsync(cancellationToken: stoppingToken);
        var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await channel.ExchangeDeclareAsync("eaat_exchange", ExchangeType.Topic, cancellationToken: stoppingToken);
        await channel.QueueDeclareAsync("courier_queue", durable: true, exclusive: false, autoDelete: false,
            cancellationToken: stoppingToken);

        // Lytter på bekræftede ordrer fra restauranten
        await channel.QueueBindAsync("courier_queue", "eaat_exchange", "order.confirmed", cancellationToken: stoppingToken);
        await channel.QueueBindAsync("courier_queue", "eaat_exchange", "courier.broadcast.taken", cancellationToken: stoppingToken);
        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (Model, ea) =>
        {
            var body = ea.Body.ToArray();
            var json = Encoding.UTF8.GetString(body);

            if (ea.RoutingKey == "order.confirmed")
            {
                var order = JsonSerializer.Deserialize<OrderConfirmed>(json);

                if (order != null)
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<CourierDbContext>();

                    // Gem ordren som et tilbud i databasen
                    db.DeliveryOffers.Add(new DeliveryOffer
                    {
                        OrderId = order.OrderId,
                        RestaurantName = order.RestaurantName,
                        IsAssigned = false
                    });

                    await db.SaveChangesAsync(stoppingToken);
                    Console.WriteLine($" [DB] Ordre #{order.OrderId} gemt i databasen og klar til bud!");
                }
            }

            if (ea.RoutingKey == "courier.broadcast.taken")
            {
                var update = JsonSerializer.Deserialize<JsonElement>(json);
                var orderId = update.GetProperty("orderId").GetString();
                
                Console.WriteLine($"[BROADCAST] Opgave #{orderId} er blevet taget af et andet bud. Fjernet fra listen...");
            }

            await channel.BasicAckAsync(ea.DeliveryTag, false, cancellationToken: stoppingToken);
        };

        await channel.BasicConsumeAsync("courier_queue", false, consumer, cancellationToken: stoppingToken);
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}

