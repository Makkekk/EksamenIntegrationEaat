using System.Text;
using System.Text.Json;
using RabbitMQ.Client;


namespace OrderService.Messaging;

public class OrderPublisher
{
    private readonly string hostname = "localhost";

    public async Task PublishOrderCreatedAsync(object orderEvent)
    {
        var factory = new ConnectionFactory
        {
            HostName = hostname
        };
        var connection = await factory.CreateConnectionAsync();
        var channel = await connection.CreateChannelAsync();

        await channel.ExchangeDeclareAsync(exchange: "eaat_exchange", type: ExchangeType.Topic);
        
        var message = JsonSerializer.Serialize(orderEvent);
        var body = Encoding.UTF8.GetBytes(message);

        await channel.BasicPublishAsync(
            exchange: "eaat_exchange",
            routingKey: "order.created",
            body: body);
        
        Console.WriteLine($" [x] Besked send: {message}" );
    }
}