namespace POS.Domain.Entities;

/// <summary>Fail-open: a category with no row here for a given branch is available by
/// default, so a newly-added branch never loses menu sections it hasn't been configured for
/// yet (OFC-System-Detailed-Spec.md section 1.1).</summary>
public class CategoryBranchAvailability
{
    public Guid Id { get; set; }
    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public Guid BranchId { get; set; }
    public bool IsAvailable { get; set; } = true;
}
