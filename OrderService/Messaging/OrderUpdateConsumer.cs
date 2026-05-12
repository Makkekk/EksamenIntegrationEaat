using System.Text;
using System.Text.Json;
using Contracts;
using OrderService.Data;
using OrderService.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace OrderService.Messaging;

public class OrderUpdateConsumer : BackgroundService
{
    private readonly string hostname = "localhost";
    private readonly IServiceScopeFactory _scopeFactory;

    public OrderUpdateConsumer(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = hostname
        };
        var connection = await factory.CreateConnectionAsync(cancellationToken: stoppingToken);
        var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
        
        await channel.ExchangeDeclareAsync(exchange: "eaat_exchange", type: ExchangeType.Topic, cancellationToken: stoppingToken);
        
        //kø til statusopdateringer
        await channel.QueueDeclareAsync(queue: "order_updates", durable: true, exclusive: false, cancellationToken: stoppingToken);
        
        //bind til flere emner med routing keys
        await channel.QueueBindAsync(queue: "order_updates", exchange: "eaat_exchange", routingKey: "order.*", cancellationToken: stoppingToken);
        await channel.QueueBindAsync(queue: "order_updates", exchange: "eaat_exchange", routingKey: "courier.*", cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        
        consumer.ReceivedAsync += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var json = Encoding.UTF8.GetString(body);

            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();

                if (ea.RoutingKey == "order.confirmed")
                {
                    var confirmed = JsonSerializer.Deserialize<OrderConfirmed>(json);
                    if (confirmed != null)
                    {
                        await UpdateDatabase(db, confirmed.OrderId, "Confirmed", confirmed.RestaurantName, null);
                        NotifyCustomer(confirmed.OrderId, $"Din mad er bekræftet af {confirmed.RestaurantName} og tilberedningen er startet!");
                    }
                }
                if (ea.RoutingKey == "courier.assigned")
                {
                    var assigned = JsonSerializer.Deserialize<CourierAssigned>(json);
                    if (assigned != null)
                    {
                        await UpdateDatabase(db, assigned.OrderId, "CourierAssigned", null, assigned.CourierName);
                    }
                }
            }
            
            await channel.BasicAckAsync(ea.DeliveryTag, false, cancellationToken: stoppingToken);
        };
        await channel.BasicConsumeAsync("order_updates", false, consumer,cancellationToken: stoppingToken);
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task UpdateDatabase(OrderDbContext db, Guid orderId, string status, string? restaurantName, string? courierName)
    {
        var order = await db.Orders.FindAsync(orderId);
        if (order != null)
        {
            // IDEMPOTENS & STATE CHECK: 
            // Hvis ordren allerede er "CourierAssigned", skal vi ikke sætte den tilbage til "Confirmed"
            if (order.Status == "CourierAssigned" && status == "Confirmed")
            {
                Console.WriteLine($" [IDEMPOTENS] Ignorerer 'Confirmed' besked da ordre #{orderId} allerede har 'CourierAssigned'.");
                return;
            }

            if (order.Status == status)
            {
                Console.WriteLine($" [IDEMPOTENS] Ordre #{orderId} er allerede i status '{status}'.");
                return;
            }

            order.Status = status;
            if (restaurantName != null) order.RestaurantName = restaurantName;
            if (courierName != null) order.CourierName = courierName;
            
            await db.SaveChangesAsync();
            Console.WriteLine($" [DATABASE] Ordre #{orderId} opdateret til: {status} i databasen.");
        }
        else
        {
            Console.WriteLine($" [DATABASE] Fejl: Ordre #{orderId} blev ikke fundet!");
        }
    }
    
    private void NotifyCustomer(Guid orderId, string message)
    {
        Console.WriteLine($"[NOTIFIKATION] Besked sendt til kunde for ordre #{orderId}: {message}" );
    }
}