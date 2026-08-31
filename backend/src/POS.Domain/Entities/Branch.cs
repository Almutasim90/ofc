namespace POS.Domain.Entities;

public class Branch
{
    public Guid Id { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public decimal DefaultOpeningFloat { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>The sale number to hand out next for this branch's receipts. Claimed
    /// atomically via an UPDATE ... RETURNING so concurrent sales never collide.</summary>
    public int NextSaleNumber { get; set; } = 1;
    public int NextOrderNumber { get; set; } = 1;
}
