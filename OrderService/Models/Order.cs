using System.ComponentModel.DataAnnotations;

namespace OrderService.Models;

public class Order
{
    [Key]
    public Guid OrderId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Status { get; set; } = "Created";
    public string? RestaurantName { get; set; }
    public string? CourierName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}