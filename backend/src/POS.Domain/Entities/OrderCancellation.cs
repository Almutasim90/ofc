namespace POS.Domain.Entities;

public class OrderCancellation
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public RestaurantOrder Order { get; set; } = null!;
    public Guid? OrderItemId { get; set; }
    public RestaurantOrderItem? OrderItem { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid CancelledByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
