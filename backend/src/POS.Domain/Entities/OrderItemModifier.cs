namespace POS.Domain.Entities;
public class OrderItemModifier { public Guid Id { get; set; } public Guid OrderItemId { get; set; } public RestaurantOrderItem OrderItem { get; set; }=null!; public Guid ModifierOptionId { get; set; } public ModifierOption ModifierOption { get; set; }=null!; public decimal PriceDeltaSnapshot { get; set; } }
