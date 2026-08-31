namespace POS.Domain.Entities;

/// <summary>One selectable slot within a combo MenuItem (e.g. "main", "side", "drink").</summary>
public class ComboComponent
{
    public Guid Id { get; set; }
    public Guid ComboMenuItemId { get; set; }
    public MenuItem ComboMenuItem { get; set; } = null!;
    public string SlotLabel { get; set; } = string.Empty;
    public bool IsRequired { get; set; } = true;
    public int MinSelect { get; set; } = 1;
    public int MaxSelect { get; set; } = 1;

    public ICollection<ComboComponentOption> Options { get; set; } = new List<ComboComponentOption>();
}
