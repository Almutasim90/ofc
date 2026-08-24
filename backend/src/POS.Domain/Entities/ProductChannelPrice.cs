namespace POS.Domain.Entities;

public class ProductChannelPrice
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public Guid ChannelId { get; set; }
    public SalesChannel Channel { get; set; } = null!;
    public decimal Price { get; set; }
}
