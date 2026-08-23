namespace POS.Domain.Entities;

/// <summary>Immutable audit trail entry for any manual stock change (e.g. goods received).</summary>
public class StockAdjustment
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }

    public Guid RawMaterialId { get; set; }
    public RawMaterial RawMaterial { get; set; } = null!;

    public decimal QuantityChange { get; set; }
    public string Reason { get; set; } = string.Empty;

    public Guid AdjustedByUserId { get; set; }
    public DateTime AdjustedAt { get; set; }
}
