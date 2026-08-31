namespace POS.Domain.Entities;

/// <summary>A single-product MenuItem selectable within a ComboComponent slot, with the
/// price difference from that slot's default choice (e.g. upsizing fries).</summary>
public class ComboComponentOption
{
    public Guid Id { get; set; }
    public Guid ComboComponentId { get; set; }
    public ComboComponent ComboComponent { get; set; } = null!;
    public Guid MenuItemId { get; set; }
    public MenuItem MenuItem { get; set; } = null!;
    public decimal PriceDelta { get; set; }
    public bool IsDefault { get; set; }
}
