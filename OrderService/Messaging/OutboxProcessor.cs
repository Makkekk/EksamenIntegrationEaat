using System.Text.Json;
using OrderService.Data;
using Microsoft.EntityFrameworkCore;

namespace OrderService.Messaging;

public class OutboxProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OrderPublisher _publisher;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(IServiceScopeFactory scopeFactory, OrderPublisher publisher, ILogger<OutboxProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _publisher = publisher;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxProcessor starter...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();

                var messages = await db.OutboxMessages
                    .Where(m => !m.IsProcessed)
                    .OrderBy(m => m.CreatedAt)
                    .Take(10)
                    .ToListAsync(stoppingToken);

                foreach (var message in messages)
                {
                    _logger.LogInformation($"Behandler Outbox besked: {message.Id} af type {message.Type}");

                    
                    var content = JsonSerializer.Deserialize<object>(message.Content);
                    
                    if (content != null)
                    {
                        await _publisher.PublishOrderCreatedAsync(content);
                        
                        message.IsProcessed = true;
                        await db.SaveChangesAsync(stoppingToken);
                        
                        _logger.LogInformation($"Besked {message.Id} markeret som Processed.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fejl under processering af Outbox.");
            }

            // Vent lidt før næste tjek
            await Task.Delay(5000, stoppingToken);
        }
    }
}
