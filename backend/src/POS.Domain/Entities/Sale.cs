using POS.Domain.Constants;

namespace POS.Domain.Entities;

public class Sale
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public Guid ChannelId { get; set; }
    public SalesChannel Channel { get; set; } = null!;

    public Guid ShiftId { get; set; }
    public Shift Shift { get; set; } = null!;

    public Guid CashierUserId { get; set; }

    /// <summary>The accounting day this sale counts against - distinct from CreatedAt so a
    /// late-night closing schedule (Sprint 5) can roll sales into the previous business day.</summary>
    public DateOnly BusinessDate { get; set; }

    public DateTime CreatedAt { get; set; }
    public decimal TotalAmount { get; set; }
    public string DiscountType { get; set; } = "None";
    public decimal DiscountValue { get; set; }
    public decimal DiscountAmount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string Status { get; set; } = SaleStatus.Completed;

    public ICollection<SaleItem> Items { get; set; } = new List<SaleItem>();
    public ICollection<SaleInventoryConsumption> InventoryConsumptions { get; set; } = new List<SaleInventoryConsumption>();
    public VoidRequest? VoidRequest { get; set; }
}
