namespace POS.Domain.Entities;

public class BillSplitLine
{
    public Guid Id { get; set; }
    public Guid BillSplitId { get; set; }
    public BillSplit BillSplit { get; set; } = null!;
    public Guid OrderItemId { get; set; }
    public RestaurantOrderItem OrderItem { get; set; } = null!;
    public int Quantity { get; set; }
}
