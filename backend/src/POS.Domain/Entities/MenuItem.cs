namespace POS.Domain.Entities;

public static class MenuItemKinds
{
    public const string SingleProduct = "SingleProduct";
    public const string Combo = "Combo";
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) { SingleProduct, Combo };
}

public class MenuItem
{
    public Guid Id { get; set; }
    public Guid CategoryId { get; set; }
    public MenuCategory Category { get; set; } = null!;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string Kind { get; set; } = MenuItemKinds.SingleProduct;
    public decimal BasePrice { get; set; }
    public string? ImageUrl { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? PrinterSectionId { get; set; }
    public ICollection<ComboComponent> ComboComponents { get; set; } = [];
    public ICollection<MenuItemModifierGroup> ModifierGroups { get; set; } = [];
}
