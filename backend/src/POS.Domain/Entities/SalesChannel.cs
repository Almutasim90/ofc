namespace POS.Domain.Entities;

public class SalesChannel
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsInStore { get; set; }
    public ICollection<ProductChannelPrice> ProductPrices { get; set; } = new List<ProductChannelPrice>();
}
