namespace POS.Domain.Entities;

public class BillSplit
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public RestaurantOrder Order { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; }
    public ICollection<BillSplitLine> Lines { get; set; } = [];
    public ICollection<OrderPayment> Payments { get; set; } = [];
}
