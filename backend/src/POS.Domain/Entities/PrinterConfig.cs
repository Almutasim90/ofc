namespace POS.Domain.Entities;

public class PrinterConfig
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public int Port { get; set; } = 9100;
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
}
