using System.Text;
using System.Text.Json;
using Contracts;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CourierService.Messaging;

public class CourierConsumer :  BackgroundService
{
    private readonly string hostname = "localhost";


    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory { HostName = hostname };
         var connection = await factory.CreateConnectionAsync(cancellationToken: stoppingToken);
         var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await channel.ExchangeDeclareAsync("eaat_exchange", ExchangeType.Topic,cancellationToken:stoppingToken);

        //kø til tilbud
        await channel.QueueDeclareAsync("courier_offers", durable: true, exclusive: false, autoDelete: false,cancellationToken: stoppingToken);
        await channel.QueueBindAsync("courier_offers", "eaat_exchange", "order.confirmed", cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (Model, ea) =>
        {
            var body = ea.Body.ToArray();
            var json = Encoding.UTF8.GetString(body);
            var confirmedOrder = JsonSerializer.Deserialize<OrderConfirmed>(json);

            if (confirmedOrder != null)
            {
                Console.WriteLine(
                    $" [x] Bud-system: Søger bud til ordre {confirmedOrder.OrderId} fra {confirmedOrder.RestaurantName}");


                //Her tildels bud
                var assigment = new CourierAssigned(confirmedOrder.OrderId, Guid.NewGuid());
                var responseBody = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(assigment));

                //Send besked om at bud er fundet
                await channel.BasicPublishAsync(
                    exchange: "eaat_exchange",
                    routingKey: "courier.assigned",
                    body: responseBody,
                    cancellationToken: stoppingToken);

                Console.WriteLine($" [v] Bud tildelt til ordre #{confirmedOrder.OrderId}");
            }

            await channel.BasicAckAsync(ea.DeliveryTag, false,cancellationToken: stoppingToken);
        };
        await channel.BasicConsumeAsync("courier_offers", false, consumer: consumer,cancellationToken: stoppingToken);
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}