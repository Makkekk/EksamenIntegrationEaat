namespace Contracts;

    public record OrderCreated(Guid OrderId, string CustomerName);
    public record OrderConfirmed(Guid OrderId, string RestaurantName);
    public record CourierAssigned(Guid OrderId, Guid CourierName);