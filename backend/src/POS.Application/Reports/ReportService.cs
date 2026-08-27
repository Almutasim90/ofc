using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Domain.Constants;

namespace POS.Application.Reports;

public class ReportService(IAppDbContext db, ICurrentUserService currentUser)
{
    public async Task<ManagerDashboardDto> GetDashboardAsync(
        DateOnly from, DateOnly to, Guid? branchId, CancellationToken cancellationToken = default)
    {
        if (to < from || to.DayNumber - from.DayNumber > 366)
            throw new ValidationException("Choose a valid reporting period of no more than 366 days.");
        if (!currentUser.BypassBranchFilter)
            branchId = currentUser.BranchId;
        else if (branchId is null && !currentUser.Permissions.Contains(PermissionKeys.ReportsGlobalView))
            throw new UnauthorizedException("Global reports permission is required.");

        var sales = db.Sales.AsNoTracking().Where(s =>
            s.BusinessDate >= from && s.BusinessDate <= to && s.Status == SaleStatus.Completed
            && (!branchId.HasValue || s.BranchId == branchId.Value));

        var dailyRows = await sales.GroupBy(s => s.BusinessDate).Select(g => new
        {
            Date = g.Key, Total = g.Sum(s => s.TotalAmount), Count = g.Count(),
            Items = g.SelectMany(s => s.Items).Sum(i => i.Quantity),
        }).OrderBy(x => x.Date).ToListAsync(cancellationToken);
        var cashDailyLookup = await sales.Where(s => s.PaymentMethod == PaymentMethods.Cash || s.PaymentMethod == PaymentMethods.Mixed)
            .GroupBy(s => s.BusinessDate).Select(g => new { Date = g.Key, Total = g.Sum(s => s.CashAmount ?? s.TotalAmount) })
            .ToDictionaryAsync(x => x.Date, x => x.Total, cancellationToken);
        var cardDailyLookup = await sales.Where(s => s.PaymentMethod == PaymentMethods.Card || s.PaymentMethod == PaymentMethods.Mixed)
            .GroupBy(s => s.BusinessDate).Select(g => new { Date = g.Key, Total = g.Sum(s => s.CardAmount ?? s.TotalAmount) })
            .ToDictionaryAsync(x => x.Date, x => x.Total, cancellationToken);
        var branchRows = await sales.GroupBy(s => s.BranchId).Select(g => new
        {
            BranchId = g.Key, Total = g.Sum(s => s.TotalAmount), Count = g.Count(),
        }).OrderByDescending(x => x.Total).ToListAsync(cancellationToken);
        var branchIds = branchRows.Select(x => x.BranchId).ToList();
        var branchLookup = await db.Branches.AsNoTracking().Where(b => branchIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, cancellationToken);
        var paymentRows = await PaymentBreakdownAsync(sales, cancellationToken);
        var productRows = await db.SaleItems.AsNoTracking().Where(i =>
            i.Sale.BusinessDate >= from && i.Sale.BusinessDate <= to && i.Sale.Status == SaleStatus.Completed
            && (!branchId.HasValue || i.Sale.BranchId == branchId.Value))
            .GroupBy(i => new { i.ProductId, i.Product.NameAr, i.Product.NameEn }).Select(g => new
            {
                g.Key.ProductId, g.Key.NameAr, g.Key.NameEn, Quantity = g.Sum(i => i.Quantity),
                Total = g.Sum(i => i.LineTotal), Invoices = g.Select(i => i.SaleId).Distinct().Count(),
            }).OrderByDescending(x => x.Quantity).ToListAsync(cancellationToken);
        // Mixed-payment sales split their total across cash and card at the sale level only, so an
        // item's share of that split is undefined; cash/card product figures cover single-method sales only.
        var cashProductLookup = await ProductPaymentBreakdownAsync(from, to, branchId, PaymentMethods.Cash, cancellationToken);
        var cardProductLookup = await ProductPaymentBreakdownAsync(from, to, branchId, PaymentMethods.Card, cancellationToken);
        var varianceRows = await db.Shifts.AsNoTracking().Where(s => s.OpenedAt >= from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
                && s.OpenedAt < to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
                && s.Status == ShiftStatus.Closed && s.VarianceAmount != null && (!branchId.HasValue || s.BranchId == branchId))
            .OrderBy(s => s.OpenedAt).Select(s => new ShiftVariancePointDto(s.Id, s.OpenedAt, s.VarianceAmount!.Value)).ToListAsync(cancellationToken);

        var totalSales = dailyRows.Sum(x => x.Total);
        var totalDiscounts = await sales.SumAsync(s => s.DiscountAmount, cancellationToken);
        var invoiceCount = dailyRows.Sum(x => x.Count);
        return new ManagerDashboardDto(from, to, totalSales, totalDiscounts, invoiceCount, dailyRows.Sum(x => x.Items),
            invoiceCount == 0 ? 0 : totalSales / invoiceCount,
            dailyRows.Select(x => new SalesTrendPointDto(x.Date, x.Total, x.Count, x.Items,
                cashDailyLookup.GetValueOrDefault(x.Date), cardDailyLookup.GetValueOrDefault(x.Date))).ToList(),
            branchRows.Select(x => new BranchSalesSummaryDto(x.BranchId, branchLookup[x.BranchId].NameAr,
                branchLookup[x.BranchId].NameEn, x.Total, x.Count)).ToList(),
            paymentRows,
            productRows.Select(x =>
            {
                var cash = cashProductLookup.GetValueOrDefault(x.ProductId);
                var card = cardProductLookup.GetValueOrDefault(x.ProductId);
                return new ProductSalesSummaryDto(x.ProductId, x.NameAr, x.NameEn, x.Quantity, x.Total, x.Invoices,
                    cash.Quantity, cash.Total, cash.Invoices,
                    card.Quantity, card.Total, card.Invoices);
            }).ToList(), varianceRows);
    }

    private async Task<Dictionary<Guid, (decimal Quantity, decimal Total, int Invoices)>> ProductPaymentBreakdownAsync(
        DateOnly from, DateOnly to, Guid? branchId, string paymentMethod, CancellationToken cancellationToken)
    {
        var rows = await db.SaleItems.AsNoTracking().Where(i =>
            i.Sale.BusinessDate >= from && i.Sale.BusinessDate <= to && i.Sale.Status == SaleStatus.Completed
            && i.Sale.PaymentMethod == paymentMethod && (!branchId.HasValue || i.Sale.BranchId == branchId.Value))
            .GroupBy(i => i.ProductId).Select(g => new
            {
                ProductId = g.Key, Quantity = g.Sum(i => i.Quantity),
                Total = g.Sum(i => i.LineTotal), Invoices = g.Select(i => i.SaleId).Distinct().Count(),
            }).ToListAsync(cancellationToken);
        return rows.ToDictionary(x => x.ProductId, x => (x.Quantity, x.Total, x.Invoices));
    }

    public async Task<DiscountReportDto> GetDiscountsAsync(DateOnly from, DateOnly to, Guid? branchId,
        Guid? cashierUserId, CancellationToken cancellationToken = default)
    {
        if (!currentUser.BypassBranchFilter) branchId = currentUser.BranchId;
        var rows = await db.Sales.AsNoTracking().Where(s => s.BusinessDate >= from && s.BusinessDate <= to
                && s.Status == SaleStatus.Completed && s.DiscountAmount > 0
                && (!branchId.HasValue || s.BranchId == branchId) && (!cashierUserId.HasValue || s.CashierUserId == cashierUserId))
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new DiscountSaleDto(s.Id, s.BranchId, s.CashierUserId, s.CreatedAt, s.DiscountAmount, s.TotalAmount))
            .ToListAsync(cancellationToken);
        return new DiscountReportDto(from, to, rows.Sum(x => x.DiscountAmount), rows);
    }

    public async Task<List<ChannelSalesDto>> GetChannelDistributionAsync(DateOnly from, DateOnly to, Guid? branchId,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.BypassBranchFilter) branchId = currentUser.BranchId;
        var rows = await db.Sales.AsNoTracking().Where(s => s.BusinessDate >= from && s.BusinessDate <= to
                && s.Status == SaleStatus.Completed && (!branchId.HasValue || s.BranchId == branchId))
            .GroupBy(s => new { s.ChannelId, s.Channel.NameAr, s.Channel.NameEn })
            .Select(g => new { g.Key.ChannelId, g.Key.NameAr, g.Key.NameEn, TotalSales = g.Sum(s => s.TotalAmount), InvoiceCount = g.Count() })
            .OrderByDescending(x => x.TotalSales).ToListAsync(cancellationToken);
        return rows.Select(x => new ChannelSalesDto(x.ChannelId, x.NameAr, x.NameEn, x.TotalSales, x.InvoiceCount)).ToList();
    }

    public async Task<DailySalesReportDto> GetDailyBranchAsync(
        Guid branchId, DateOnly date, CancellationToken cancellationToken = default)
    {
        EnsureBranchScope(branchId);
        var branch = await db.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == branchId, cancellationToken)
            ?? throw new NotFoundException("Branch not found.");
        var sales = db.Sales.AsNoTracking().Where(s =>
            s.BranchId == branchId && s.BusinessDate == date && s.Status == SaleStatus.Completed);
        var breakdown = await PaymentBreakdownAsync(sales, cancellationToken);
        return new(branch.Id, branch.NameAr, branch.NameEn, date,
            await sales.SumAsync(s => s.TotalAmount, cancellationToken), await sales.CountAsync(cancellationToken), breakdown);
    }

    private static async Task<List<PaymentBreakdownDto>> PaymentBreakdownAsync(IQueryable<POS.Domain.Entities.Sale> sales, CancellationToken cancellationToken)
    {
        var cash = sales.Where(s => s.PaymentMethod == PaymentMethods.Cash || s.PaymentMethod == PaymentMethods.Mixed);
        var card = sales.Where(s => s.PaymentMethod == PaymentMethods.Card || s.PaymentMethod == PaymentMethods.Mixed);
        return [
            new(PaymentMethods.Cash, await cash.SumAsync(s => s.CashAmount ?? s.TotalAmount, cancellationToken), await cash.CountAsync(cancellationToken)),
            new(PaymentMethods.Card, await card.SumAsync(s => s.CardAmount ?? s.TotalAmount, cancellationToken), await card.CountAsync(cancellationToken)),
        ];
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
