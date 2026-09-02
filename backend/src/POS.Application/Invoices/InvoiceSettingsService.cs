using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Domain.Entities;

namespace POS.Application.Invoices;

public record InvoiceSettingsDto(Guid BranchId, string LegalNameAr, string LegalNameEn, string? TaxRegistrationNumber, string? CommercialRegistrationNumber, string? AddressAr, string? AddressEn, string? Phone, string Currency, bool PricesIncludeTax, decimal DefaultTaxRate, string? Footer);
public record SaveInvoiceSettingsRequest(Guid BranchId, string LegalNameAr, string LegalNameEn, string? TaxRegistrationNumber, string? CommercialRegistrationNumber, string? AddressAr, string? AddressEn, string? Phone, string Currency, bool PricesIncludeTax, decimal DefaultTaxRate, string? Footer);

public class InvoiceSettingsService(IAppDbContext db, ICurrentUserService currentUser)
{
    public async Task<InvoiceSettingsDto> GetAsync(Guid branchId, CancellationToken ct = default)
    {
        branchId = Scope(branchId);
        var row = await db.InvoiceSettings.AsNoTracking().SingleOrDefaultAsync(x => x.BranchId == branchId, ct);
        if (row is not null) return ToDto(row);
        var branch = await db.Branches.AsNoTracking().SingleOrDefaultAsync(x => x.Id == branchId, ct) ?? throw new NotFoundException("Branch not found.");
        return new(branchId, branch.NameAr, branch.NameEn, null, null, null, null, null, "OMR", false, 0, null);
    }

    public async Task<InvoiceSettingsDto> SaveAsync(SaveInvoiceSettingsRequest request, CancellationToken ct = default)
    {
        var branchId = Scope(request.BranchId);
        Validate(request);
        if (!await db.Branches.AnyAsync(x => x.Id == branchId, ct)) throw new NotFoundException("Branch not found.");
        var row = await db.InvoiceSettings.SingleOrDefaultAsync(x => x.BranchId == branchId, ct);
        if (row is null) { row = new() { Id = Guid.NewGuid(), BranchId = branchId }; db.InvoiceSettings.Add(row); }
        row.LegalNameAr = request.LegalNameAr.Trim(); row.LegalNameEn = request.LegalNameEn.Trim();
        row.TaxRegistrationNumber = Clean(request.TaxRegistrationNumber); row.CommercialRegistrationNumber = Clean(request.CommercialRegistrationNumber);
        row.AddressAr = Clean(request.AddressAr); row.AddressEn = Clean(request.AddressEn); row.Phone = Clean(request.Phone);
        row.Currency = request.Currency.Trim().ToUpperInvariant(); row.PricesIncludeTax = request.PricesIncludeTax; row.DefaultTaxRate = request.DefaultTaxRate; row.Footer = Clean(request.Footer);
        await db.SaveChangesAsync(ct);
        return ToDto(row);
    }

    private Guid Scope(Guid requested)
    {
        if (!currentUser.BypassBranchFilter && requested != currentUser.BranchId) throw new ForbiddenException("You do not have access to this branch.");
        return currentUser.BypassBranchFilter ? requested : currentUser.BranchId ?? throw new ValidationException("A branch assignment is required.");
    }

    private static void Validate(SaveInvoiceSettingsRequest x)
    {
        if (string.IsNullOrWhiteSpace(x.LegalNameAr) || x.LegalNameAr.Trim().Length > 200 || string.IsNullOrWhiteSpace(x.LegalNameEn) || x.LegalNameEn.Trim().Length > 200) throw new ValidationException("Arabic and English legal names are required and limited to 200 characters.");
        if (x.Currency.Trim().Length != 3) throw new ValidationException("Currency must be a three-letter code.");
        if (x.DefaultTaxRate is < 0 or > 100) throw new ValidationException("Default tax rate must be between 0 and 100.");
        if (x.TaxRegistrationNumber?.Trim().Length > 100 || x.CommercialRegistrationNumber?.Trim().Length > 100 || x.AddressAr?.Trim().Length > 500 || x.AddressEn?.Trim().Length > 500 || x.Phone?.Trim().Length > 50 || x.Footer?.Trim().Length > 1000) throw new ValidationException("One or more invoice fields exceed their maximum length.");
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static InvoiceSettingsDto ToDto(InvoiceSettings x) => new(x.BranchId, x.LegalNameAr, x.LegalNameEn, x.TaxRegistrationNumber, x.CommercialRegistrationNumber, x.AddressAr, x.AddressEn, x.Phone, x.Currency, x.PricesIncludeTax, x.DefaultTaxRate, x.Footer);
}
