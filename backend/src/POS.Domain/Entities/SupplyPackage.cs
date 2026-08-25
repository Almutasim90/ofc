namespace POS.Domain.Entities;

public class SupplyPackage
{
    public Guid Id { get; set; }
    public Guid RawMaterialId { get; set; }
    public RawMaterial RawMaterial { get; set; } = null!;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public decimal BaseQuantity { get; set; }
    public bool IsActive { get; set; } = true;
}
