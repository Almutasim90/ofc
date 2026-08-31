namespace POS.Domain.Entities;

public class ComboComponent
{
    public Guid Id { get; set; }
    public Guid ComboMenuItemId { get; set; }
    public MenuItem ComboMenuItem { get; set; } = null!;
    public string SlotLabel { get; set; } = string.Empty;
    public bool IsRequired { get; set; } = true;
    public int MinSelect { get; set; } = 1;
    public int MaxSelect { get; set; } = 1;
    public int SortOrder { get; set; }
    public ICollection<ComboComponentOption> Options { get; set; } = [];
}
