namespace POS.Domain.Entities;

/// <summary>
/// A recipe line for a product at a given branch. A product with zero recipe rows
/// (for a branch) sells with no stock deduction at all - used for fresh/prepared-to-order
/// items that aren't tracked as pre-made quantity (e.g. beer, nuts).
/// </summary>
public class ProductRecipe
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public Guid BranchId { get; set; }

    public Guid RawMaterialId { get; set; }
    public RawMaterial RawMaterial { get; set; } = null!;

    public decimal QuantityRequired { get; set; }
}
