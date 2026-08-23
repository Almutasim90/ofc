namespace POS.Domain.Entities;

/// <summary>Inventory usage snapshot captured at sale time so reports and voids remain
/// correct even if a branch recipe changes later.</summary>
public class SaleInventoryConsumption
{
    public Guid Id { get; set; }
    public Guid SaleId { get; set; }
    public Sale Sale { get; set; } = null!;
    public Guid RawMaterialId { get; set; }
    public RawMaterial RawMaterial { get; set; } = null!;
    public decimal QuantityConsumed { get; set; }
}
