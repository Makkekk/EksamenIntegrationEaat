using System.ComponentModel.DataAnnotations;

namespace CourierService.Models;

public class OutboxMessage
{
    [Key]
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty; // F.eks. "CourierAssigned" eller "CourierBroadcastTaken"
    public string RoutingKey { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty; 
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsProcessed { get; set; } = false;
}
