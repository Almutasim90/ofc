namespace POS.Domain.Entities;

public class PrinterSection
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public Guid? PrinterConfigId { get; set; }
    public PrinterConfig? PrinterConfig { get; set; }
}
