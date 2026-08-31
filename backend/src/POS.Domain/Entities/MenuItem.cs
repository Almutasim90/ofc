using POS.Domain.Constants;

namespace POS.Domain.Entities;

/// <summary>Shared row for both a single sellable product and a combo/meal (Kind decides
/// which) so "Offers" and "Kids Meal" are ordinary categories full of ordinary MenuItems -
/// never a special-cased table (OFC-System-Detailed-Spec.md section 1.2).</summary>
public class MenuItem
{
    public Guid Id { get; set; }
    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string Kind { get; set; } = MenuItemKind.SingleProduct;

    /// <summary>For a combo, this is the combo's own price - not the sum of its components.</summary>
    public decimal BasePrice { get; set; }
    public string? ImageUrl { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<ComboComponent> ComboComponents { get; set; } = new List<ComboComponent>();
}
