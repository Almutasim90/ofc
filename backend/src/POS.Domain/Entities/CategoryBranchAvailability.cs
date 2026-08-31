namespace POS.Domain.Entities;

public class CategoryBranchAvailability
{
    public Guid Id { get; set; }
    public Guid CategoryId { get; set; }
    public MenuCategory Category { get; set; } = null!;
    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public bool IsAvailable { get; set; } = true;
}
