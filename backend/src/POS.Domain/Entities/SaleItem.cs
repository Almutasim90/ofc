namespace POS.Domain.Entities;

public class SaleItem
{
    public string? RecipeSnapshotJson { get; set; }
    public Guid Id { get; set; }

    public Guid SaleId { get; set; }
    public Sale Sale { get; set; } = null!;

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;

    /// <summary>Snapshot at sale time - never a live lookup, so later renaming/repricing
    /// the product doesn't rewrite history.</summary>
    public string ProductNameSnapshot { get; set; } = string.Empty;
    public decimal UnitPriceSnapshot { get; set; }

    public decimal Quantity { get; set; }
    public decimal LineTotal { get; set; }
    public string DiscountType { get; set; } = "None";
    public decimal DiscountValue { get; set; }
}
