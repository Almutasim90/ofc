namespace POS.Domain.Entities;

public class Table
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public string Label { get; set; } = string.Empty;
    public int? Capacity { get; set; }
    public bool IsActive { get; set; } = true;
}
