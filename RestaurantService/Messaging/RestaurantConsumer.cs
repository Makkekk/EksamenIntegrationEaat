using System.Text;
using System.Text.Json;
using Contracts;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace RestaurantService.Messaging;

public class RestaurantConsumer : BackgroundService
{
    private readonly string hostname = "localhost";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory()
        {
            HostName = hostname
        };

        var connection = await factory.CreateConnectionAsync(stoppingToken);
        var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await channel.ExchangeDeclareAsync(exchange: "eaat_exchange", type: ExchangeType.Topic,
            cancellationToken: stoppingToken);

        await channel.QueueDeclareAsync(queue: "restaurant_order", durable: true, exclusive: false, autoDelete: false,
            cancellationToken: stoppingToken);
        await channel.QueueBindAsync("restaurant_order", "eaat_exchange", "order.created",
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var json = Encoding.UTF8.GetString(body);

            var order = JsonSerializer.Deserialize<OrderCreated>(json);

            if (order != null)
            {
                Console.WriteLine($" [x] Restaurant modtog ordre: #{order.OrderId} til {order.CustomerName}");


                var confirmed = new OrderConfirmed(order.OrderId, "Pizza Palace");
                var response = JsonSerializer.Serialize(confirmed);
                var responseBody = Encoding.UTF8.GetBytes(response);


                await channel.BasicPublishAsync(
                    exchange: "eaat_exchange",
                    routingKey: "order.confirmed",
                    body: responseBody,
                    cancellationToken: stoppingToken);


                Console.WriteLine($" [v] Restaurant sendte bekræftelse for #{order.OrderId} til {order.CustomerName}");
            }

            await channel.BasicAckAsync(ea.DeliveryTag, false, cancellationToken: stoppingToken);
        };


        await channel.BasicConsumeAsync("restaurant_order", autoAck: false, consumer: consumer,
            cancellationToken: stoppingToken);

        //Vent til applikationen lukker ned
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}