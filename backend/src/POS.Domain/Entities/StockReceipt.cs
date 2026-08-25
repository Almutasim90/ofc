namespace POS.Domain.Entities;

public class StockReceipt
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public Guid RawMaterialId { get; set; }
    public RawMaterial RawMaterial { get; set; } = null!;
    public Guid SupplyPackageId { get; set; }
    public SupplyPackage SupplyPackage { get; set; } = null!;
    public decimal PackageCount { get; set; }
    public decimal BaseQuantityAdded { get; set; }
    public string PackageNameSnapshot { get; set; } = string.Empty;
    public string? Note { get; set; }
    public Guid ReceivedByUserId { get; set; }
    public DateTime ReceivedAt { get; set; }
}
