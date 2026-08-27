namespace POS.Domain.Entities;

public class BranchRawMaterialStock
{
    public uint Version { get; set; }
    public Guid BranchId { get; set; }

    public Guid RawMaterialId { get; set; }
    public RawMaterial RawMaterial { get; set; } = null!;

    public decimal CurrentQuantity { get; set; }
    public decimal LowStockThreshold { get; set; }
}
