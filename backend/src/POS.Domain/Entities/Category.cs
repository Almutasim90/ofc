namespace POS.Domain.Entities;

public class Category
{
    public Guid Id { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<CategoryBranchAvailability> BranchAvailability { get; set; } = new List<CategoryBranchAvailability>();
    public ICollection<MenuItem> Items { get; set; } = new List<MenuItem>();
}
