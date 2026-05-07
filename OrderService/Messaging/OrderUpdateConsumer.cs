using System.Text;
using System.Text.Json;
using Contracts;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace OrderService.Messaging;

public class OrderUpdateConsumer : BackgroundService
{
    private readonly string hostname = "localhost";

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

            if (ea.RoutingKey == "order.confirmed")
            {
                var confirmed = JsonSerializer.Deserialize<OrderConfirmed>(json);
                UpdateDatabase(confirmed.OrderId, "Confirmed by" + confirmed.RestaurantName);
                
                NotifyCustomer(confirmed.OrderId, $"Din mad er bekræftet af {confirmed.RestaurantName} og tilberedningen er startet!");
            }
            if (ea.RoutingKey == "courier.assigned")
            {
                var assigned = JsonSerializer.Deserialize<CourierAssigned>(json);
                UpdateDatabase(assigned.OrderId, "Courier assigned: " + assigned.CourierName);
            }
            
            await channel.BasicAckAsync(ea.DeliveryTag, false, cancellationToken: stoppingToken);
        };
        await channel.BasicConsumeAsync("order_updates", false, consumer,cancellationToken: stoppingToken);
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private void UpdateDatabase(Guid orderId, string status)
    {
        Console.WriteLine($" [DATABASE] Ordre #{orderId} opdateret til: {status}");
    }
    private void  NotifyCustomer(Guid orderId, string message)
    {Console.WriteLine($"[NOTIFIKATION] Besked sendt til kunde for ordre #{orderId}: {message}" );}
}