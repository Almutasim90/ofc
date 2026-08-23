namespace POS.Domain.Entities;

public class VoidRequest
{
    public Guid Id { get; set; }
    public Guid SaleId { get; set; }
    public Sale Sale { get; set; } = null!;
    public Guid RequestedByUserId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid? ApprovedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
