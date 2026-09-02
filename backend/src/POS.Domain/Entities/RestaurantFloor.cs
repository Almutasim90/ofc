namespace POS.Domain.Entities;

public class RestaurantFloor
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<RestaurantTable> Tables { get; set; } = [];
}
