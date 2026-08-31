namespace POS.Domain.Entities;

public class BranchFeatureFlag
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public string FeatureKey { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
}
