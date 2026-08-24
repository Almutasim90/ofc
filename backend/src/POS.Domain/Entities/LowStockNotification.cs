namespace POS.Domain.Entities;

public class LowStockNotification
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public Guid RawMaterialId { get; set; }
    public RawMaterial RawMaterial { get; set; } = null!;
    public DateTime TriggeredAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
