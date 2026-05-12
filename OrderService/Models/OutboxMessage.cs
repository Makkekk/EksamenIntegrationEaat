using System.ComponentModel.DataAnnotations;

namespace OrderService.Models;

public class OutboxMessage
{
    [Key]
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty; 
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsProcessed { get; set; } = false;
}
