namespace POS.Domain.Entities;

public class Product
{
    public Guid Id { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? IconOrImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
}
