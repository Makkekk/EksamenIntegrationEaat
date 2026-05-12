using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using CourierService.Models;

namespace CourierService.Data;

public class CourierDbContext : DbContext
{
    public CourierDbContext(DbContextOptions<CourierDbContext> options) : base(options) { }

    public DbSet<DeliveryOffer> DeliveryOffers => Set<DeliveryOffer>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
}

public class DeliveryOffer
{
    [Key]
    public Guid OrderId { get; set; }
    public string RestaurantName { get; set; } = string.Empty;
    public bool IsAssigned { get; set; }
    public string? CourierName { get; set; }
}
