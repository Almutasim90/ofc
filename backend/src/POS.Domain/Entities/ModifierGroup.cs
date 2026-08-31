namespace POS.Domain.Entities;

public class ModifierGroup
{
    public Guid Id { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public int MinSelect { get; set; }
    public int MaxSelect { get; set; } = 1;
    public bool IsRequired { get; set; }
    public ICollection<ModifierOption> Options { get; set; } = [];
    public ICollection<MenuItemModifierGroup> MenuItems { get; set; } = [];
}
