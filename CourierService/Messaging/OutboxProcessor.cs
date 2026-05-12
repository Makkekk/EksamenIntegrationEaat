using System.Text.Json;
using CourierService.Data;
using Microsoft.EntityFrameworkCore;

namespace CourierService.Messaging;

public class OutboxProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly CourierPublisher _publisher;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(IServiceScopeFactory scopeFactory, CourierPublisher publisher, ILogger<OutboxProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _publisher = publisher;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Courier OutboxProcessor starter...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<CourierDbContext>();

                var messages = await db.OutboxMessages
                    .Where(m => !m.IsProcessed)
                    .OrderBy(m => m.CreatedAt)
                    .Take(10)
                    .ToListAsync(stoppingToken);

                foreach (var message in messages)
                {
                    _logger.LogInformation($"Behandler Courier Outbox besked: {message.Id} af type {message.Type}");

                    await _publisher.PublishAsync(message.RoutingKey, message.Content);
                    
                    message.IsProcessed = true;
                    await db.SaveChangesAsync(stoppingToken);
                    
                    _logger.LogInformation($"Besked {message.Id} markeret som Processed.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fejl under processering af Courier Outbox.");
            }

            await Task.Delay(5000, stoppingToken);
        }
    }
}
