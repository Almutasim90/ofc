namespace POS.Domain.Entities;

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
