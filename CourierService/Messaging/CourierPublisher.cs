using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace CourierService.Messaging;

public class CourierPublisher
{
    private readonly string hostname = "localhost";

    public async Task PublishAsync(string routingKey, string content)
    {
        var factory = new ConnectionFactory
        {
            HostName = hostname
        };
        using var connection = await factory.CreateConnectionAsync();
        using var channel = await connection.CreateChannelAsync();

        await channel.ExchangeDeclareAsync(exchange: "eaat_exchange", type: ExchangeType.Topic);
        
        var body = Encoding.UTF8.GetBytes(content);

        await channel.BasicPublishAsync(
            exchange: "eaat_exchange",
            routingKey: routingKey,
            body: body);
        
        Console.WriteLine($" [OUTBOX] Besked sendt med routing key '{routingKey}': {content}");
    }
}
