using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Domain.Constants;
using POS.Domain.Entities;

namespace POS.Application.Reports;

public class ReportService(IAppDbContext db, ICurrentUserService currentUser)
{
    public async Task<ManagerDashboardDto> GetDashboardAsync(DateOnly from, DateOnly to, Guid? branchId, CancellationToken ct = default)
    {
        ValidatePeriod(from, to);
        branchId = Scope(branchId);
        var orders = ReportableOrders(from, to, branchId);

        var dailyRows = await orders.GroupBy(x => x.BusinessDate).Select(g => new
        {
            Date = g.Key,
            Total = g.Sum(x => x.GrandTotal),
            Count = g.Count(),
            Items = g.SelectMany(x => x.Items).Where(x => !x.IsCancelled).Sum(x => (decimal)x.Quantity),
        }).OrderBy(x => x.Date).ToListAsync(ct);
        var paymentDaily = await db.OrderPayments.AsNoTracking().Where(x =>
                x.Order.BusinessDate >= from && x.Order.BusinessDate <= to && ReportableStatuses.Contains(x.Order.Status)
                && (!branchId.HasValue || x.Order.BranchId == branchId))
            .GroupBy(x => new { x.Order.BusinessDate, x.PaymentMethod.Code })
            .Select(g => new { g.Key.BusinessDate, g.Key.Code, Total = g.Sum(x => x.Amount) }).ToListAsync(ct);
        var branchRows = await orders.GroupBy(x => x.BranchId)
            .Select(g => new { BranchId = g.Key, Total = g.Sum(x => x.GrandTotal), Count = g.Count() })
            .OrderByDescending(x => x.Total).ToListAsync(ct);
        var branchIds = branchRows.Select(x => x.BranchId).ToList();
        var branchLookup = await db.Branches.AsNoTracking().Where(x => branchIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var paymentRows = await PaymentBreakdownAsync(from, to, branchId, ct);
        var productRows = await db.RestaurantOrderItems.AsNoTracking().Where(x => !x.IsCancelled
                && x.Order.BusinessDate >= from && x.Order.BusinessDate <= to && ReportableStatuses.Contains(x.Order.Status)
                && (!branchId.HasValue || x.Order.BranchId == branchId))
            .GroupBy(x => new { x.MenuItemId, x.MenuItem.NameAr, x.MenuItem.NameEn })
            .Select(g => new { ProductId = g.Key.MenuItemId, g.Key.NameAr, g.Key.NameEn, Quantity = g.Sum(x => (decimal)x.Quantity), Total = g.Sum(x => x.LineTotal), Invoices = g.Select(x => x.OrderId).Distinct().Count() })
            .OrderByDescending(x => x.Quantity).ToListAsync(ct);
        var cashProducts = await ProductBySingleMethod(from, to, branchId, "CASH", ct);
        var cardProducts = await ProductBySingleMethod(from, to, branchId, "CARD", ct);
        var start = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var end = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var cashVariances = await db.CashShifts.AsNoTracking().Where(x => x.OpenedAt >= start && x.OpenedAt < end && x.Status == CashShiftStatuses.Closed && (!branchId.HasValue || x.BranchId == branchId))
            .OrderByDescending(x => x.OpenedAt).Select(x => new CashShiftVarianceDto(x.Id, x.BranchId, x.OpenedAt, x.ExpectedCash ?? 0, x.CountedCash ?? 0, x.VarianceCash ?? 0)).ToListAsync(ct);
        var edits = await db.OrderEditLogs.AsNoTracking().Where(x => x.CreatedAt >= start && x.CreatedAt < end && (!branchId.HasValue || x.Order.BranchId == branchId))
            .OrderByDescending(x => x.CreatedAt).Select(x => new OrderEditReportDto(x.Id, x.OrderId, x.Order.OrderNumber, x.Order.BranchId, x.UserId, x.EditType, x.Notes, x.AmountDelta, x.CreatedAt)).ToListAsync(ct);
        var orderTypeRows = await orders.GroupBy(x => new { x.OrderType.Code, x.OrderType.NameAr, x.OrderType.NameEn })
            .Select(g => new { g.Key.Code, g.Key.NameAr, g.Key.NameEn, Total = g.Sum(x => x.GrandTotal), Count = g.Count() }).ToListAsync(ct);
        var orderTypes = orderTypeRows.OrderByDescending(x => x.Total).Select(x => new OrderTypeSalesDto(x.Code, x.NameAr, x.NameEn, x.Total, x.Count)).ToList();

        var total = dailyRows.Sum(x => x.Total);
        var invoices = dailyRows.Sum(x => x.Count);
        return new(from, to, total, await orders.SumAsync(x => x.DiscountAmount, ct), invoices, dailyRows.Sum(x => x.Items), invoices == 0 ? 0 : total / invoices,
            dailyRows.Select(x => new SalesTrendPointDto(x.Date, x.Total, x.Count, x.Items,
                paymentDaily.Where(p => p.BusinessDate == x.Date && p.Code == "CASH").Sum(p => p.Total),
                paymentDaily.Where(p => p.BusinessDate == x.Date && p.Code == "CARD").Sum(p => p.Total))).ToList(),
            branchRows.Select(x => new BranchSalesSummaryDto(x.BranchId, branchLookup[x.BranchId].NameAr, branchLookup[x.BranchId].NameEn, x.Total, x.Count)).ToList(),
            paymentRows,
            productRows.Select(x => { var cash = cashProducts.GetValueOrDefault(x.ProductId); var card = cardProducts.GetValueOrDefault(x.ProductId); return new ProductSalesSummaryDto(x.ProductId, x.NameAr, x.NameEn, x.Quantity, x.Total, x.Invoices, cash.Quantity, cash.Total, cash.Invoices, card.Quantity, card.Total, card.Invoices); }).ToList(),
            [], cashVariances, edits, orderTypes);
    }

    public async Task<DiscountReportDto> GetDiscountsAsync(DateOnly from, DateOnly to, Guid? branchId, Guid? cashierUserId, CancellationToken ct = default)
    {
        ValidatePeriod(from, to); branchId = Scope(branchId);
        var rows = await ReportableOrders(from, to, branchId).Where(x => x.DiscountAmount > 0 && (!cashierUserId.HasValue || x.CashierUserId == cashierUserId))
            .OrderByDescending(x => x.CreatedAt).Select(x => new DiscountSaleDto(x.Id, x.BranchId, x.CashierUserId, x.CreatedAt, x.DiscountAmount, x.GrandTotal)).ToListAsync(ct);
        return new(from, to, rows.Sum(x => x.DiscountAmount), rows);
    }

    public async Task<List<ChannelSalesDto>> GetChannelDistributionAsync(DateOnly from, DateOnly to, Guid? branchId, CancellationToken ct = default)
    {
        ValidatePeriod(from, to); branchId = Scope(branchId);
        var rows = await ReportableOrders(from, to, branchId).Where(x => x.SalesChannelId != null)
            .GroupBy(x => x.SalesChannelId!.Value).Select(g => new { Id = g.Key, Total = g.Sum(x => x.GrandTotal), Count = g.Count() }).ToListAsync(ct);
        var ids = rows.Select(x => x.Id).ToList();
        var channels = await db.SalesChannels.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        return rows.OrderByDescending(x => x.Total).Select(x => new ChannelSalesDto(x.Id, channels[x.Id].NameAr, channels[x.Id].NameEn, x.Total, x.Count)).ToList();
    }

    public async Task<DailySalesReportDto> GetDailyBranchAsync(Guid branchId, DateOnly date, CancellationToken ct = default)
    {
        EnsureBranchScope(branchId);
        var branch = await db.Branches.AsNoTracking().FirstOrDefaultAsync(x => x.Id == branchId, ct) ?? throw new NotFoundException("Branch not found.");
        var orders = ReportableOrders(date, date, branchId);
        return new(branch.Id, branch.NameAr, branch.NameEn, date, await orders.SumAsync(x => x.GrandTotal, ct), await orders.CountAsync(ct), await PaymentBreakdownAsync(date, date, branchId, ct));
    }

    public async Task<GlobalSalesReportDto> GetGlobalAsync(DateOnly date, CancellationToken ct = default)
    {
        if (!currentUser.Permissions.Contains(PermissionKeys.ReportsGlobalView)) throw new UnauthorizedException("Global reports permission is required.");
        var rows = await ReportableOrders(date, date, null).GroupBy(x => x.BranchId).Select(g => new { BranchId = g.Key, Total = g.Sum(x => x.GrandTotal), Count = g.Count() }).ToListAsync(ct);
        var ids = rows.Select(x => x.BranchId).ToList(); var branches = await db.Branches.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var summaries = rows.Select(x => new BranchSalesSummaryDto(x.BranchId, branches[x.BranchId].NameAr, branches[x.BranchId].NameEn, x.Total, x.Count)).OrderByDescending(x => x.TotalSales).ToList();
        return new(date, summaries.Sum(x => x.TotalSales), summaries.Sum(x => x.InvoiceCount), summaries);
    }

    public async Task<ShiftInventoryReportDto> GetShiftInventoryAsync(Guid shiftId, CancellationToken ct = default)
    {
        var shift = await db.Shifts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == shiftId, ct) ?? throw new NotFoundException("Legacy shift not found.");
        var rows = await db.SaleInventoryConsumptions.AsNoTracking().Where(x => x.Sale.ShiftId == shiftId && x.Sale.Status == SaleStatus.Completed)
            .GroupBy(x => new { x.RawMaterialId, x.RawMaterial.NameAr, x.RawMaterial.NameEn, x.RawMaterial.Unit })
            .Select(g => new InventoryConsumptionDto(g.Key.RawMaterialId, g.Key.NameAr, g.Key.NameEn, g.Key.Unit, g.Sum(x => x.QuantityConsumed))).OrderBy(x => x.NameEn).ToListAsync(ct);
        return new(shift.Id, shift.BranchId, rows);
    }

    private async Task<List<PaymentBreakdownDto>> PaymentBreakdownAsync(DateOnly from, DateOnly to, Guid? branchId, CancellationToken ct)
    {
        var rows = await db.OrderPayments.AsNoTracking().Where(x => x.Order.BusinessDate >= from && x.Order.BusinessDate <= to && ReportableStatuses.Contains(x.Order.Status) && (!branchId.HasValue || x.Order.BranchId == branchId))
            .GroupBy(x => x.PaymentMethod.Code).Select(g => new { Method = g.Key, Total = g.Sum(x => x.Amount), Invoices = g.Select(x => x.OrderId).Distinct().Count() }).ToListAsync(ct);
        return rows.OrderBy(x => x.Method).Select(x => new PaymentBreakdownDto(x.Method, x.Total, x.Invoices)).ToList();
    }

    private async Task<Dictionary<Guid, (decimal Quantity, decimal Total, int Invoices)>> ProductBySingleMethod(DateOnly from, DateOnly to, Guid? branchId, string method, CancellationToken ct)
    {
        var rows = await db.RestaurantOrderItems.AsNoTracking().Where(x => !x.IsCancelled && x.Order.BusinessDate >= from && x.Order.BusinessDate <= to && ReportableStatuses.Contains(x.Order.Status) && (!branchId.HasValue || x.Order.BranchId == branchId)
                && x.Order.Payments.Any(p => p.PaymentMethod.Code == method) && !x.Order.Payments.Any(p => p.PaymentMethod.Code != method))
            .GroupBy(x => x.MenuItemId).Select(g => new { Id = g.Key, Quantity = g.Sum(x => (decimal)x.Quantity), Total = g.Sum(x => x.LineTotal), Invoices = g.Select(x => x.OrderId).Distinct().Count() }).ToListAsync(ct);
        return rows.ToDictionary(x => x.Id, x => (x.Quantity, x.Total, x.Invoices));
    }

    private IQueryable<RestaurantOrder> ReportableOrders(DateOnly from, DateOnly to, Guid? branchId) => db.RestaurantOrders.AsNoTracking().Where(x => x.BusinessDate >= from && x.BusinessDate <= to && ReportableStatuses.Contains(x.Status) && (!branchId.HasValue || x.BranchId == branchId));
    private static readonly string[] ReportableStatuses = [RestaurantOrderStatuses.Paid, RestaurantOrderStatuses.Closed];
    private Guid? Scope(Guid? branchId) { if (!currentUser.BypassBranchFilter) return currentUser.BranchId; if (branchId is null && !currentUser.Permissions.Contains(PermissionKeys.ReportsGlobalView)) throw new UnauthorizedException("Global reports permission is required."); return branchId; }
    private static void ValidatePeriod(DateOnly from, DateOnly to) { if (to < from || to.DayNumber - from.DayNumber > 366) throw new ValidationException("Choose a valid reporting period of no more than 366 days."); }
    private void EnsureBranchScope(Guid branchId) { if (!currentUser.BypassBranchFilter && branchId != currentUser.BranchId) throw new ValidationException("You do not have access to this branch report."); }
}
