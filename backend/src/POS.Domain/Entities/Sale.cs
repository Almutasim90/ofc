using POS.Domain.Constants;

namespace POS.Domain.Entities;

public class Sale
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }

    /// <summary>Null until Sprint 4 wires shifts; every sale will require an open shift then.</summary>
    public Guid? ShiftId { get; set; }

    public Guid CashierUserId { get; set; }

    /// <summary>The accounting day this sale counts against - distinct from CreatedAt so a
    /// late-night closing schedule (Sprint 5) can roll sales into the previous business day.</summary>
    public DateOnly BusinessDate { get; set; }

    public DateTime CreatedAt { get; set; }
    public decimal TotalAmount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string Status { get; set; } = SaleStatus.Completed;

    public ICollection<SaleItem> Items { get; set; } = new List<SaleItem>();
}
