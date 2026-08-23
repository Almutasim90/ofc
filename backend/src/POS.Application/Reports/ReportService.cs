using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Domain.Constants;

namespace POS.Application.Reports;

public class ReportService(IAppDbContext db, ICurrentUserService currentUser)
{
    public async Task<DailySalesReportDto> GetDailyBranchAsync(
        Guid branchId, DateOnly date, CancellationToken cancellationToken = default)
    {
        EnsureBranchScope(branchId);
        var branch = await db.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == branchId, cancellationToken)
            ?? throw new NotFoundException("Branch not found.");
        var sales = db.Sales.AsNoTracking().Where(s =>
            s.BranchId == branchId && s.BusinessDate == date && s.Status == SaleStatus.Completed);
        var paymentRows = await sales.GroupBy(s => s.PaymentMethod)
            .Select(g => new { PaymentMethod = g.Key, TotalAmount = g.Sum(s => s.TotalAmount), InvoiceCount = g.Count() })
            .OrderBy(x => x.PaymentMethod).ToListAsync(cancellationToken);
        var breakdown = paymentRows.Select(x => new PaymentBreakdownDto(x.PaymentMethod, x.TotalAmount, x.InvoiceCount)).ToList();
        return new(branch.Id, branch.NameAr, branch.NameEn, date,
            breakdown.Sum(x => x.TotalAmount), breakdown.Sum(x => x.InvoiceCount), breakdown);
    }

    public async Task<GlobalSalesReportDto> GetGlobalAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        if (!currentUser.Permissions.Contains(PermissionKeys.ReportsGlobalView))
            throw new UnauthorizedException("Global reports permission is required.");
        var rows = await db.Sales.AsNoTracking()
            .Where(s => s.BusinessDate == date && s.Status == SaleStatus.Completed)
            .GroupBy(s => s.BranchId)
            .Select(g => new { BranchId = g.Key, Total = g.Sum(s => s.TotalAmount), Count = g.Count() })
            .ToListAsync(cancellationToken);
        var branchIds = rows.Select(r => r.BranchId).ToList();
        var branches = await db.Branches.AsNoTracking().Where(b => branchIds.Contains(b.Id)).ToListAsync(cancellationToken);
        var summaries = rows.Select(row =>
        {
            var branch = branches.First(b => b.Id == row.BranchId);
            return new BranchSalesSummaryDto(branch.Id, branch.NameAr, branch.NameEn, row.Total, row.Count);
        }).OrderByDescending(x => x.TotalSales).ToList();
        return new(date, summaries.Sum(x => x.TotalSales), summaries.Sum(x => x.InvoiceCount), summaries);
    }

    public async Task<ShiftInventoryReportDto> GetShiftInventoryAsync(
        Guid shiftId, CancellationToken cancellationToken = default)
    {
        var shift = await db.Shifts.AsNoTracking().FirstOrDefaultAsync(s => s.Id == shiftId, cancellationToken)
            ?? throw new NotFoundException("Shift not found.");
        var rows = await db.SaleInventoryConsumptions.AsNoTracking()
            .Where(c => c.Sale.ShiftId == shiftId && c.Sale.Status == SaleStatus.Completed)
            .GroupBy(c => new { c.RawMaterialId, c.RawMaterial.NameAr, c.RawMaterial.NameEn, c.RawMaterial.Unit })
            .Select(g => new { g.Key.RawMaterialId, g.Key.NameAr, g.Key.NameEn, g.Key.Unit, Quantity = g.Sum(c => c.QuantityConsumed) })
            .OrderBy(x => x.NameEn).ToListAsync(cancellationToken);
        var materials = rows.Select(x => new InventoryConsumptionDto(
            x.RawMaterialId, x.NameAr, x.NameEn, x.Unit, x.Quantity)).ToList();
        return new(shift.Id, shift.BranchId, materials);
    }

    private void EnsureBranchScope(Guid branchId)
    {
        if (!currentUser.BypassBranchFilter && branchId != currentUser.BranchId)
            throw new ValidationException("You do not have access to this branch report.");
    }
}
