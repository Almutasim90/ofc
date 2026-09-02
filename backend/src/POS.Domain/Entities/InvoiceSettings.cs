namespace POS.Domain.Entities;

public class InvoiceSettings
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public string LegalNameAr { get; set; } = string.Empty;
    public string LegalNameEn { get; set; } = string.Empty;
    public string? TaxRegistrationNumber { get; set; }
    public string? CommercialRegistrationNumber { get; set; }
    public string? AddressAr { get; set; }
    public string? AddressEn { get; set; }
    public string? Phone { get; set; }
    public string Currency { get; set; } = "OMR";
    public bool PricesIncludeTax { get; set; }
    public decimal DefaultTaxRate { get; set; }
    public string? Footer { get; set; }
}
