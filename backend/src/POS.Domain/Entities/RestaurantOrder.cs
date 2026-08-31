namespace POS.Domain.Entities;
public static class RestaurantOrderStatuses { public const string Open="Open", Sent="Sent", Paid="Paid", Closed="Closed", Cancelled="Cancelled"; public static readonly IReadOnlySet<string> All=new HashSet<string>{Open,Sent,Paid,Closed,Cancelled}; }
public class RestaurantOrder
{
    public Guid Id { get; set; } public Guid BranchId { get; set; } public Branch Branch { get; set; }=null!; public int OrderNumber { get; set; }
    public Guid OrderTypeId { get; set; } public OrderType OrderType { get; set; }=null!; public Guid? TableId { get; set; } public RestaurantTable? Table { get; set; }
    public string? CarPlateNumber { get; set; } public Guid? CashierUserId { get; set; } public Guid? CashShiftId { get; set; }
    public DateOnly BusinessDate { get; set; } public DateTime CreatedAt { get; set; } public decimal Subtotal { get; set; } public decimal DiscountAmount { get; set; }
    public decimal GrandTotal { get; set; } public string Status { get; set; }=RestaurantOrderStatuses.Open; public Guid? SalesChannelId { get; set; } public Guid? OrderingSessionId { get; set; }
    public ICollection<RestaurantOrderItem> Items { get; set; }=[];
    public ICollection<OrderCancellation> Cancellations { get; set; }=[];
    public ICollection<OrderPayment> Payments { get; set; }=[];
    public ICollection<OrderEditLog> EditLogs { get; set; }=[];
}
